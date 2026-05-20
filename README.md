# Cinema Booking System

Distributed microservices cinema booking platform built with **.NET 8** and **Clean Architecture**. Implements **CQRS** (SQL Server write + MongoDB read), **Saga Orchestration** with compensation, **Transactional Outbox** via Kafka, **Consul** service discovery, and **JWT** authentication. Full observability via **OpenTelemetry** (Seq, Jaeger, Prometheus). Deployable to Docker Compose or Kubernetes with HPA-based autoscaling.

---

## Architecture Diagram

```mermaid
graph TB
    Client([Client Apps])

    subgraph "API Layer"
        GW[API Gateway<br/>YARP + JWT :5005]
        LB[Load Balancer<br/>:5003]
    end

    subgraph "Service Discovery"
        Consul[(Consul :8500)]
    end

    subgraph "Write Side - CQRS"
        API1[Cinema API :5001]
        API2[Cinema API :5002]
        SQL[(SQL Server)]
        Outbox[Outbox Table]
        API1 & API2 --> SQL
        API1 & API2 --> Outbox
    end

    subgraph "Event Processing - Saga"
        Master[Master Node<br/>Outbox Processor]
        Kafka{{Kafka :9092}}
        Saga[Ticket Purchase Saga<br/>1.Reserve → 2.Pay → 3.Confirm → 4.Issue]
    end

    subgraph "Read Side - CQRS"
        ReadSvc[Read Service<br/>gRPC]
        Mongo[(MongoDB)]
        Redis[(Redis Cache)]
    end

    subgraph "Bounded Contexts - DDD"
        BC[Reservation • Payment<br/>Ticket • Showtime]
    end

    subgraph "Observability - OpenTelemetry"
        Seq[Seq<br/>Logs :5341]
        Jaeger[Jaeger<br/>Traces :16686]
        Prom[Prometheus<br/>Metrics :9090]
    end

    Client -->|HTTPS| GW
    GW --> LB
    LB --> API1 & API2

    API1 & API2 -.->|Register| Consul
    LB -.->|Discover| Consul

    Master -->|Poll| Outbox
    Master -->|Publish| Kafka
    Kafka -->|Consume| ReadSvc
    ReadSvc --> Mongo
    ReadSvc -.-> Redis

    GW -->|gRPC Query| ReadSvc

    API1 & API2 -.-> BC
    Saga -.->|Orchestrates| BC

    API1 & API2 & GW & ReadSvc & Master -.->|Logs/Traces/Metrics| Seq & Jaeger & Prom

    style Consul fill:#f9f,stroke:#333
    style Kafka fill:#ff9,stroke:#333
    style SQL fill:#9cf,stroke:#333
    style Mongo fill:#9f9,stroke:#333
    style Saga fill:#fc9,stroke:#333
```

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


