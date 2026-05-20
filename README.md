# Cinema Booking System

Distributed microservices cinema booking platform built with **.NET 8** and **Clean Architecture**. Implements **CQRS** (SQL Server write + MongoDB read), **Saga Orchestration** with compensation, **Transactional Outbox** via Kafka, **Consul** service discovery, and **JWT** authentication. Full observability via **OpenTelemetry** (Seq, Jaeger, Prometheus). Deployable to Docker Compose or Kubernetes with HPA-based autoscaling.

---

## Architecture Diagram

```mermaid
flowchart TB
    Client([👤 Client Apps]):::client

    subgraph EDGE[" 🌐 EDGE LAYER "]
        direction LR
        GW["🚪 API Gateway<br/><b>YARP + JWT</b><br/>:5005"]:::gateway
        LB["⚖️ Load Balancer<br/><b>YARP</b><br/>:5003"]:::gateway
    end

    subgraph DISCOVERY[" 🧭 SERVICE DISCOVERY "]
        Consul[("🔷 Consul<br/>:8500")]:::discovery
    end

    subgraph WRITE[" ✍️ WRITE SIDE (CQRS) "]
        direction TB
        API1["Cinema API #1<br/>:5001"]:::api
        API2["Cinema API #2<br/>:5002"]:::api
        SQL[("🗄️ SQL Server")]:::db
        Outbox[/"📤 Outbox Table"/]:::outbox
    end

    subgraph EVENTS[" ⚡ EVENT PROCESSING "]
        direction TB
        Master["⚙️ Master Node<br/>Outbox Processor"]:::worker
        Kafka{{"📨 Kafka Bus<br/>:9092"}}:::kafka
        Saga["🔄 <b>Ticket Saga</b><br/>1️⃣ Reserve → 2️⃣ Pay<br/>3️⃣ Confirm → 4️⃣ Issue"]:::saga
    end

    subgraph DDD[" 🧩 BOUNDED CONTEXTS (DDD) "]
        BC["📦 Reservation  •  Payment<br/>Ticket  •  Showtime"]:::ddd
    end

    subgraph READ[" 📖 READ SIDE (CQRS) "]
        direction TB
        ReadSvc["📡 Read Service<br/><b>gRPC</b>"]:::api
        Mongo[("🍃 MongoDB")]:::db
        Redis[("⚡ Redis Cache")]:::cache
    end

    subgraph OBS[" 👁️ OBSERVABILITY (OpenTelemetry) "]
        direction LR
        Seq["📝 <b>Seq</b><br/>Logs :5341"]:::obs
        Jaeger["🔍 <b>Jaeger</b><br/>Traces :16686"]:::obs
        Prom["📊 <b>Prometheus</b><br/>Metrics :9090"]:::obs
    end

    %% ===== MAIN REQUEST FLOW =====
    Client ==>|"① HTTPS"| GW
    GW ==>|"② Route"| LB
    LB ==>|"③ Forward"| API1
    LB ==>|"③ Forward"| API2
    API1 ==>|"④ Write"| SQL
    API2 ==>|"④ Write"| SQL
    API1 ==>|"④ Event"| Outbox
    API2 ==>|"④ Event"| Outbox

    %% ===== EVENT PIPELINE =====
    Master ==>|"⑤ Poll"| Outbox
    Master ==>|"⑥ Publish"| Kafka
    Kafka ==>|"⑦ Consume"| ReadSvc
    ReadSvc ==>|"⑧ Project"| Mongo
    ReadSvc -.->|"Cache"| Redis

    %% ===== QUERY PATH =====
    GW -->|"🔎 gRPC Query"| ReadSvc

    %% ===== SERVICE DISCOVERY =====
    API1 -.->|"register"| Consul
    API2 -.->|"register"| Consul
    LB -.->|"discover"| Consul

    %% ===== DOMAIN =====
    API1 -.-> BC
    API2 -.-> BC
    Saga -.->|"orchestrates"| BC
    Master --- Saga

    %% ===== OBSERVABILITY =====
    EDGE -.->|"📡 OTel"| OBS
    WRITE -.->|"📡 OTel"| OBS
    READ -.->|"📡 OTel"| OBS
    EVENTS -.->|"📡 OTel"| OBS

    %% ===== STYLES =====
    classDef client fill:#E3F2FD,stroke:#1565C0,stroke-width:3px,color:#0D47A1
    classDef gateway fill:#FFF3E0,stroke:#E65100,stroke-width:2px,color:#BF360C
    classDef discovery fill:#F8BBD0,stroke:#AD1457,stroke-width:2px,color:#880E4F
    classDef api fill:#C8E6C9,stroke:#2E7D32,stroke-width:2px,color:#1B5E20
    classDef db fill:#BBDEFB,stroke:#1565C0,stroke-width:2px,color:#0D47A1
    classDef cache fill:#FFCDD2,stroke:#C62828,stroke-width:2px,color:#B71C1C
    classDef outbox fill:#FFE0B2,stroke:#E65100,stroke-width:2px,color:#BF360C
    classDef worker fill:#D1C4E9,stroke:#4527A0,stroke-width:2px,color:#311B92
    classDef kafka fill:#FFF59D,stroke:#F57F17,stroke-width:3px,color:#F57F17
    classDef saga fill:#FFAB91,stroke:#BF360C,stroke-width:3px,color:#BF360C
    classDef ddd fill:#B2DFDB,stroke:#00695C,stroke-width:2px,color:#004D40
    classDef obs fill:#E1BEE7,stroke:#6A1B9A,stroke-width:2px,color:#4A148C

    style EDGE fill:#FAFAFA,stroke:#616161,stroke-width:2px
    style DISCOVERY fill:#FCE4EC,stroke:#AD1457,stroke-width:2px
    style WRITE fill:#E8F5E9,stroke:#2E7D32,stroke-width:2px
    style READ fill:#E8F5E9,stroke:#2E7D32,stroke-width:2px
    style EVENTS fill:#FFF8E1,stroke:#F57F17,stroke-width:2px
    style DDD fill:#E0F2F1,stroke:#00695C,stroke-width:2px
    style OBS fill:#F3E5F5,stroke:#6A1B9A,stroke-width:2px
```

> **Legend:** `══►` main request flow  •  `──►` query/sync call  •  `┄┄►` async/registration/telemetry

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


