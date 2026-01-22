# Cinema Booking System

A distributed microservices cinema booking platform built with **.NET 8** and **Clean Architecture**.

## Key Patterns

| Pattern | Description |
|---------|-------------|
| **CQRS** | SQL Server (write) + MongoDB (read) |
| **Saga Orchestration** | Distributed transactions with compensation |
| **Transactional Outbox** | Guaranteed event delivery via Kafka |
| **Service Discovery** | Consul-based dynamic registration |
| **JWT Authentication** | Role-based authorization |

## Tech Stack

**.NET 8** | **SQL Server** | **MongoDB** | **Redis** | **Kafka** | **Consul** | **YARP** | **gRPC**

---

## Diagram 1: Architecture and Data Flow

```mermaid
graph TB
    subgraph "Clients"
        Client[Client App]
    end

    subgraph "API Layer"
        GW[API Gateway :5005]
        LB[Load Balancer :5003]
    end

    subgraph "Service Discovery"
        Consul[Consul :8500]
    end

    subgraph "Write Side"
        API1[Cinema API :5001]
        API2[Cinema API :5002]
        SQL[(SQL Server)]
        Outbox[Outbox Table]
    end

    subgraph "Event Processing"
        Master[Master Node]
        Kafka[Kafka :9092]
    end

    subgraph "Read Side"
        ReadSvc[Read Service gRPC]
        Mongo[(MongoDB)]
        Redis[(Redis)]
    end

    Client -->|1. HTTP Request| GW
    GW -->|2. Route| LB
    LB -->|3. Forward| API1 & API2
    API1 & API2 -->|4. Write + Outbox| SQL

    API1 & API2 -.->|Register| Consul
    LB -.->|Discover| Consul

    Master -->|5. Poll| Outbox
    Master -->|6. Publish| Kafka
    Kafka -->|7. Consume| ReadSvc
    ReadSvc -->|8. Update| Mongo
    ReadSvc -.->|Cache| Redis

    GW -->|GET via gRPC| ReadSvc

    style Consul fill:#f9f,stroke:#333
    style Kafka fill:#ff9,stroke:#333
    style SQL fill:#9cf,stroke:#333
    style Mongo fill:#9f9,stroke:#333
```

**Data Flow:**
1. Client sends request to API Gateway (JWT authenticated)
2. Gateway routes through Load Balancer
3. Load Balancer discovers healthy instances via Consul
4. API writes data + outbox message (atomic transaction)
5. Master Node polls outbox for new messages
6. Events published to Kafka
7. Read Service consumes events
8. MongoDB updated, Redis cache invalidated

---

## Diagram 2: DDD Bounded Contexts and Saga

```mermaid
graph TB
    subgraph "Ticket Purchase Saga"
        direction LR
        Start((Start)) --> S1[1. Reserve Seats]
        S1 --> S2[2. Process Payment]
        S2 --> S3[3. Confirm Reservation]
        S3 --> S4[4. Issue Ticket]
        S4 --> Done((Done))

        S1 -.->|Fail| C1[Release Seats]
        S2 -.->|Fail| C2[Release Seats]
        S3 -.->|Fail| C3[Refund + Release]
        S4 -.->|Fail| C4[Cancel All]
    end

    subgraph "Bounded Contexts"
        subgraph "Reservation Context"
            R[Reservation Aggregate]
            RS[ReservationSeat]
            R --> RS
        end

        subgraph "Payment Context"
            P[Payment Aggregate]
            PM[PaymentMethod]
            P --> PM
        end

        subgraph "Ticket Context"
            T[Ticket Aggregate]
            TN[TicketNumber]
            T --> TN
        end

        subgraph "Showtime Context"
            SH[Showtime Aggregate]
            M[MovieId]
            SH --> M
        end
    end

    subgraph "Infrastructure"
        Events[Domain Events]
        Outbox[(Outbox)]
        Bus[Kafka Bus]

        R & P & T & SH -.->|Emit| Events
        Events --> Outbox
        Outbox --> Bus
    end

    S1 --> R
    S2 --> P
    S3 --> R
    S4 --> T

    style R fill:#fc9,stroke:#333
    style P fill:#9f9,stroke:#333
    style T fill:#ff9,stroke:#333
    style SH fill:#9cf,stroke:#333
```

**Saga Steps:**

| Step | Action | Compensation |
|------|--------|--------------|
| 1 | Reserve Seats | Release Seats |
| 2 | Process Payment | Refund Payment |
| 3 | Confirm Reservation | Cancel Reservation |
| 4 | Issue Ticket | Void Ticket |

---

## Quick Start

```bash
# Start infrastructure
docker-compose -f docker-compose.infrastructure.yml up -d

# Start services
docker-compose up -d
```

## Services

| Service | URL |
|---------|-----|
| API Gateway | http://localhost:5005 |
| Consul UI | http://localhost:8500 |
| Swagger | http://localhost:8080/swagger |
| Kafka UI | http://localhost:8090 |

## Project Structure

```
src/
├── Cinema.Api/           # Write API
├── Cinema.ApiGateway/    # YARP Gateway
├── Cinema.LoadBalancer/  # Load Balancer + Consul
├── Cinema.ReadService/   # gRPC Read Service
├── Cinema.MasterNode/    # Outbox Processor
├── Cinema.Application/   # CQRS + Sagas
├── Cinema.Domain/        # DDD Aggregates
├── Cinema.Infrastructure/# EF Core, Kafka, Consul
└── Cinema.Contracts/     # DTOs
```

## License

MIT
