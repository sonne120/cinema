# Cinema Booking System

Distributed microservices cinema booking platform built with **.NET 8** and **Clean Architecture**. Implements **CQRS** (SQL Server write + MongoDB read), **Saga Orchestration** with compensation, **Transactional Outbox** via Kafka, **Consul** service discovery, and **JWT** authentication. Full observability via **OpenTelemetry** (Seq, Jaeger, Prometheus). Deployable to Docker Compose or Kubernetes with HPA-based autoscaling.

---

## Architecture

```
  ═══ WRITE PATH:  clients → gateway → load balancer → Cinema API ×2 → SQL Server outbox ═══

                         ┌────────────────────────────────┐
                         │          Client apps           │  HTTPS · JWT bearer
                         │       web · mobile · CLI       │  commands + queries
                         └───────────────┬────────────────┘
                                         ▼
                         ┌────────────────────────────────┐
                         │  API Gateway  ·  :5005         │  single entry point
                         │  Ocelot routing · JWT auth     │  REST in, gRPC out
                         │  Polly retry + circuit breaker │  Swagger UI
                         │  gRPC clients: write · read    │
                         └────┬───────────────────────┬───┘
     write (gRPC / h2c)       │                       └────▶  read (gRPC) — jumps
                              ▼                               to Cinema gRPC :7080
   ┌────────────────┐    ┌────────────────────────────────┐   in the READ PATH
   │  Consul :8500  │───▶│  LoadBalancer  ·  :5003        │  route table built
   │                │    │  YARP reverse proxy            │  from Consul, not
   │  service       │    │  round-robin over live pods    │  from static config
   │  registry +    │    └───────────────┬────────────────┘
   │  health checks │                    ▼
   │                │    ┌────────────────────────────────┐
   │                │◀───│  Cinema API × 2 :5001 / :5002  │  Clean Architecture
   └────────────────┘    │  REST + gRPC · MediatR · DDD   │  CQRS write side
        register         │  Ticket Saga orchestrator      │
                         └───────────────┬────────────────┘
                                         │ one transaction: state + event
                                         ▼
                         ┌────────────────────────────────┐
                         │  SQL Server  ·  :1433          │  CinemaDb
                         │  aggregates · saga state       │  write model
                         │  OutboxMessages (transactional)│
                         └───────────────┬────────────────┘
                                         │ claim batch (READPAST, UPDLOCK)
                                         ▼
                         ┌────────────────────────────────┐
                         │  MasterNode  ·  :5010          │  polls the outbox
                         │  outbox processor              │  of both API
                         │  TPL batches · delete on ack   │  instances
                         └────┬──────────────────────┬────┘
                   project    │                      │  publish
                              ▼                      ▼
              ┌───────────────────┐   ┌────────────────────────────────┐
              │  MasterDb (SQL)   │   │  Kafka  ·  :9092               │
              │  reservations     │   │  cinema.reservations           │
              │  showtimes        │   │  cinema.showtimes              │
              └───────────────────┘   │  cinema.domain.events          │
                                      └───────────────┬────────────────┘
                                                      │
  ═══ READ PATH (CQRS):  Kafka → ReadService ×3 → MongoDB rs0 → gRPC query service ═══
                                                      │ consume · manual commit
                                                      ▼
                         ┌────────────────────────────────┐
                         │  ReadService × 3               │  group
                         │  Kafka consumer workers        │  cinema-read-
                         │  project → read models         │  service-group
                         └───────────────┬────────────────┘
                                         ▼  upsert
                         ┌────────────────────────────────┐
                         │  MongoDB replica set  rs0      │  CinemaReadDb
                         │  mongo1 :27017 (primary)       │  showtimes ·
                         │  mongo2 :27018 · mongo3 :27019 │  reservations
                         └───────────────┬────────────────┘
                                         │ query
   API Gateway                           ▼
   read (gRPC) ────────▶ ┌────────────────────────────────┐
                         │  Cinema gRPC  ·  :7080 → 5001  │  serves the
                         │  read-only query service       │  gateway's queries
                         │  HTTP/2 · never writes         │  straight from Mongo
                         └────────────────────────────────┘

  ═══ OBSERVABILITY:  every service exports logs, traces and metrics (OpenTelemetry) ═══

     ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
     │  Seq  ·  :5341       │  │  Jaeger  ·  :16686   │  │  Prometheus  ·  :9090│
     │  structured logs     │  │  OTLP traces :4317   │  │  scrapes /metrics    │
     │  (Serilog sink)      │  │  (OpenTelemetry)     │  │  from every service  │
     └──────────────────────┘  └──────────────────────┘  └──────────────────────┘
```

0. **Ingress.** Clients hit the **API Gateway** (`:5005`) — **Ocelot** routing, **JWT** bearer
   validation, Swagger, and **Polly** retry + circuit breaker around two gRPC clients: a *write*
   client pointed at the load balancer and a *read* client pointed at `cinema-grpc`.
1. **Load balancing & discovery.** Write calls go over gRPC (HTTP/2 plaintext) to the
   **LoadBalancer** (`:5003`), a **YARP** proxy whose route table is built from **Consul**
   (`ConsulProxyConfigProvider`): the API instances register themselves, the balancer discovers
   the healthy ones and round-robins across them.
2. **Write side.** **Cinema API ×2** (`:5001` / `:5002`) serves REST *and* gRPC over the same
   Clean Architecture stack (MediatR handlers → domain aggregates). Each command writes the
   aggregate change **and** the integration event into `OutboxMessages` **in one SQL Server
   transaction** — no dual-write, nothing is lost if Kafka is down.
3. **Saga.** Ticket purchase is orchestrated by `TicketPurchaseSaga` — four steps with
   compensation, saga state persisted in SQL Server (see [Ticket purchase saga](#ticket-purchase-saga)).
4. **Outbox relay.** **MasterNode** (`:5010`) polls the outbox of *both* API instances, claiming
   batches with `WITH (READPAST, UPDLOCK, ROWLOCK)` so replicas never grab the same rows. Each
   batch is fanned out with TPL (`Parallel.ForEachAsync`, grouped by event type): rows are
   projected into the master SQL database *and* published to Kafka, then deleted. A failed batch
   is released (`ProcessedOnUtc = NULL` + error) and retried.
5. **Kafka.** Events are keyed by aggregate id and routed by type to `cinema.reservations`,
   `cinema.showtimes` or `cinema.domain.events`, with `event-id` / `event-type` /
   `aggregate-type` / `aggregate-id` headers.
6. **Read side (CQRS).** **ReadService ×3** consume `cinema.domain.events` in the
   `cinema-read-service-group` (auto-commit off — the offset is committed **only after** the
   projection succeeds) and upsert denormalized documents into the **MongoDB replica set**
   (`CinemaReadDb`: showtimes, reservations).
7. **Queries.** **Cinema gRPC** (`:7080`) serves read-only queries from MongoDB over HTTP/2; the
   gateway calls it directly, so query traffic never touches SQL Server or the write path.

### Ticket purchase saga

Orchestrated saga with per-step compensation, replayed in reverse for steps that actually ran:

```
   ┌────────────────┐  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐
   │ ① Reserve seats│─▶│ ② Take payment │─▶│ ③ Confirm resv │─▶│ ④ Issue ticket │
   └───────┬────────┘  └───────┬────────┘  └───────┬────────┘  └───────┬────────┘
           │                   │                   │                   │
           ▼                   ▼                   ▼                   ▼
     release seats       refund via          revert the          cancel the
     + expire resv       payment gateway     confirmation        issued ticket
           ▲                   ▲                   ▲                   │
           └───────────────────┴───────────────────┴───────────────────┘
              compensation runs in reverse order (failure or timeout)
```

All four steps succeed → saga **Completed**; any step failure or a timeout → compensation runs
backwards and the saga ends **Compensated**. State and step progress live in SQL Server
(`SagaStateRepository`), and every step emits a saga event on the bus.

---

## Quick Start

```bash
docker-compose -f docker-compose.infrastructure.yml up -d
docker-compose up -d
```

| Service | URL |
|---------|-----|
| API Gateway | http://localhost:5005 |
| Consul UI | http://localhost:8500 |
| Swagger | http://localhost:8080/swagger |
| Kafka UI | http://localhost:8090 |
| Seq (Logs) | http://localhost:5341 |
| Jaeger (Traces) | http://localhost:16686 |
| Prometheus | http://localhost:9090 |

## Kubernetes

```bash
kubectl apply -k k8s/
kubectl get pods -n cinema
```
