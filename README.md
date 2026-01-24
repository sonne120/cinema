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

**.NET 8** | **SQL Server** | **MongoDB** | **Redis** | **Kafka** | **Consul** | **YARP** | **gRPC** | **OpenTelemetry**

## Observability Stack

The system implements a full observability solution using **OpenTelemetry** standards:

| Component | Purpose | Tool | Port (UI) |
|-----------|---------|------|------|
| **Logging** | Centralized structured logging | **Seq** | `:5341` |
| **Tracing** | Distributed request tracing | **Jaeger** | `:16686` |
| **Metrics** | Infrastructure & App metrics | **Prometheus** | `:9090` |

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

## Diagram 3: Observability Pipeline

```mermaid
graph LR
    subgraph "Application"
        API[Api Services]
        GW[Gateway]
        Nodes[Worker Nodes]
    end

    subgraph "Agents/Exporters"
        OTEL[OpenTelemetry SDK]
        Seri[Serilog Sink]
    end

    subgraph "Observability Backend"
        Seq[Seq (Logs)]
        Jaeger[Jaeger (Traces)]
        Prom[Prometheus (Metrics)]
    end
    
    subgraph "Visualization"
        Graf[Grafana]
        SeqUI[Seq UI]
        JaegerUI[Jaeger UI]
    end

    API & GW & Nodes --> OTEL
    API & GW & Nodes --> Seri
    
    Seri -->|Push Logs| Seq
    OTEL -->|Push Traces| Jaeger
    Prom -->|Scrape Metrics| API & GW & Nodes

    Seq --> SeqUI
    Jaeger --> JaegerUI
    Prom --> Graf
    
    style Seq fill:#f9f,stroke:#333
    style Jaeger fill:#9cf,stroke:#333
    style Prom fill:#ff9,stroke:#333
```

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

---

## Kubernetes Deployment

The project includes Kubernetes manifests for production deployment with native load balancing.

### Diagram 3: Kubernetes Architecture with Load Balancing

```mermaid
graph TD
    Client([Client Apps])
    
    subgraph Kubernetes Cluster
        Ingress["NGINX Ingress Controller"]
        
        subgraph "Namespace: cinema"
            Gateway["API Gateway<br>(Hybrid: Ocelot + BFF)"]
            LB["Client-Side Load Balancer<br>(YARP + Consul)"]
            
            Registry[("Consul<br>Service Registry")]
            
            subgraph Data Plane
                WriteAPI["Cinema.Api<br>(Write / gRPC + REST)"]
                ReadAPI["Cinema.ReadService<br>(Read / gRPC)"]
                Master["MasterNode Worker<br>(Background Jobs)"]
            end
            
            subgraph Persistence
                SQL[("SQL Server")]
                Mongo[("MongoDB")]
                Redis[("Redis")]
                Kafka{"Kafka Message Bus"}
            end
        end
    end

    %% Traffic Flow
    Client -->|HTTPS /api| Ingress
    Ingress -->|Route /api/*| Gateway
    
    %% Gateway Routing Logic
    Gateway -->|1. Login REST Pass-through| LB
    Gateway -->|2. Commands gRPC BFF| LB
    
    %% Load Balancing
    LB -.->|Polls IPs| Registry
    WriteAPI -.->|Registers Pod IP| Registry
    LB -->|Routes to Pod IP| WriteAPI
    
    %% Internal Comms
    Gateway -->|Queries gRPC| ReadAPI
    WriteAPI -->|Events| Kafka
    Master -->|Consumes| Kafka
    
    %% Data Access
    WriteAPI --> SQL
    ReadAPI --> Mongo
    ReadAPI --> Redis
```

### How Kubernetes Load Balancing Works

**1. External Load Balancer (Layer 4)**
```
Internet → Cloud Load Balancer → Ingress Controller
```
- Cloud providers (AWS/Azure/GCP) provision a Layer 4 load balancer
- Distributes traffic across Ingress Controller pods

**2. Ingress Controller (Layer 7)**
```
Ingress → Service (ClusterIP)
```
- Nginx Ingress handles SSL termination, path routing
- Routes `/api/*` to `api-gateway-service`

**3. Kubernetes Service (kube-proxy)**
```
Service → Pod endpoints (Round Robin)
```
- `cinema-api-service` load balances across all API pods
- Uses iptables/IPVS rules for round-robin distribution
- Health checks via readiness probes

**4. Horizontal Pod Autoscaler (HPA)**
```
CPU/Memory metrics → Scale pods 3-10
```
- Automatically scales API pods based on load
- Maintains minimum 3 replicas for high availability

### Kubernetes vs Docker Compose Load Balancing

| Aspect | Docker Compose | Kubernetes |
|--------|---------------|------------|
| **Load Balancer** | Custom YARP + Consul | Native Service + kube-proxy |
| **Service Discovery** | Consul | Built-in DNS + Endpoints |
| **Auto-scaling** | Manual | HPA (automatic) |
| **Health Checks** | Custom | Liveness/Readiness probes |
| **SSL Termination** | Application | Ingress Controller |
| **Rolling Updates** | Manual | Built-in strategy |

### Deploy to Kubernetes

```bash
# Create namespace and deploy all resources
kubectl apply -k k8s/

# Or deploy individually
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/infrastructure.yaml
kubectl apply -f k8s/observability.yaml
kubectl apply -f k8s/cinema-api.yaml
kubectl apply -f k8s/api-gateway.yaml
kubectl apply -f k8s/read-service.yaml
kubectl apply -f k8s/master-node.yaml
kubectl apply -f k8s/ingress.yaml

# Check deployment status
kubectl get pods -n cinema
kubectl get services -n cinema
kubectl get hpa -n cinema

# View logs
kubectl logs -f deployment/cinema-api -n cinema

# Scale manually (if needed)
kubectl scale deployment cinema-api --replicas=5 -n cinema
```

### K8s Files Structure

```
k8s/
├── namespace.yaml      # Namespace definition
├── configmap.yaml      # Configuration data
├── secrets.yaml        # Sensitive data (base64)
├── infrastructure.yaml # SQL, MongoDB, Redis, Kafka
├── cinema-api.yaml     # API + HPA + PDB
├── api-gateway.yaml    # Gateway + LoadBalancer Service
├── read-service.yaml   # Read Service + HPA
├── master-node.yaml    # Outbox processor
├── ingress.yaml        # Ingress + Network Policy
└── kustomization.yaml  # Kustomize config
```
