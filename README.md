# 🎬 Cinema Booking System - CQRS + Outbox + Saga Pattern

A distributed cinema booking system implementing **CQRS**, **DDD**, **Transactional Outbox Pattern**, and the **Saga Orchestration Pattern** for guaranteed event delivery and distributed transaction management.

## 🏗️ Architecture Overview
```mermaid
graph TD
    subgraph "Write Side - Transactional"
        Gateway["🌐 API Gateway<br/>Port 5005"]
        LB["⚖️ Load Balancer<br/>Port 5003"]
        API1["⚙️ API Node 1<br/>Port 5001"]
        API2["⚙️ API Node 2<br/>Port 5002"]
        SQL["💾 SQL Server<br/>Write DB (CinemaDb)"]
        
        note_trans["📝 Atomic Transaction:<br/>1. Business Data<br/>2. Outbox Message"]
    end
    
    subgraph "The Bridge - Master Node"
        MasterNode["👷 Master Node Worker<br/>(Outbox Processor)"]
        MasterSQL["💾 SQL Server<br/>Master DB (Reporting)"]
        
        note_tpl["⚡ TPL Batching<br/>Parallel.ForEachAsync"]
    end
    
    subgraph "Event Streaming"
        Kafka["📨 Kafka Broker<br/>Port 9092"]
        Topic1["Topic: cinema.domain.events"]
    end
    
    subgraph "Read Side - Queries"
        ReadService["🚀 Read Service<br/>(Kafka Consumer)"]
        
        subgraph "MongoDB Replica Set"
            Mongo1["🍃 Primary"]
            Mongo2["🍃 Secondary"]
            Mongo3["🍃 Secondary"]
        end

        Redis["⚡ Redis<br/>Cache"]
    end

    %% Command Flow
    Gateway -->|POST/PUT| LB
    LB --> API1
    LB --> API2
    
    API1 -->|Write| SQL
    API2 -->|Write| SQL
    note_trans -.-> SQL

    %% The Outbox Pattern (Master Node)
    MasterNode -->|1. Poll READPAST| SQL
    MasterNode -->|2a. Project| MasterSQL
    MasterNode -->|2b. Publish| Kafka
    note_tpl -.-> MasterNode
    
    %% Event Flow
    Kafka -->|Stream| Topic1
    Topic1 -.->|Consume| ReadService
    
    %% Read Side Updates
    ReadService -->|Update View| Mongo1
    Mongo1 -.->|Replicate| Mongo2
    Mongo1 -.->|Replicate| Mongo3
    
    %% Query Flow
    Gateway -->|GET gRPC| ReadService
    ReadService -->|Query| Mongo1
    ReadService -.->|Cache| Redis
    
    %% Styling
    style MasterNode fill:#ffccff,stroke:#660066,stroke-width:3px
    style SQL fill:#99ccff
    style MasterSQL fill:#99ccff
    style Kafka fill:#ffe6cc,stroke:#cc6600,stroke-width:2px
    style Mongo1 fill:#90ee90,stroke:#006400,stroke-width:2px
    style Redis fill:#ff6b6b,stroke:#c92a2a,stroke-width:2px
```

## 🔄 Complete Data Flow

The following diagram shows the end-to-end data flow from user request to query response:
```mermaid
graph TD
    %% ---------------------------------------------------------
    %% ACTORS & ENTRY POINTS
    %% ---------------------------------------------------------
    User((👤 User))
    Gateway["🌐 API Gateway"]
    LoadBalancer["⚖️ Load Balancer"]
    
    %% ---------------------------------------------------------
    %% WRITE SIDE (TRANSACTIONAL)
    %% ---------------------------------------------------------
    subgraph WriteBlock["Write Side (Cinema API)"]
        API["⚙️ Cinema API Node"]
        
        subgraph Transaction["Atomic Transaction"]
            direction TB
            Step1["1. Write Reservation Data"]
            Step2["2. Write Outbox Message"]
        end
        
        SQL[("💾 SQL Server (CinemaDb)<br/>Tables: Reservations, OutboxMessages")]
    end

    %% ---------------------------------------------------------
    %% ASYNC PROCESSING (MASTER NODE)
    %% ---------------------------------------------------------
    subgraph MasterBlock["Async Processing (Master Node)"]
        Poller["🔄 Poller Thread"]
        Channel["⚡ Memory Channel"]
        Worker["👷 Worker Thread (TPL)"]
        
        MasterDB[("💾 Master DB (Reporting)")]
    end

    %% ---------------------------------------------------------
    %% EVENT STREAMING
    %% ---------------------------------------------------------
    Kafka["📨 Kafka Topic: cinema.reservations"]

    %% ---------------------------------------------------------
    %% READ SIDE (QUERIES)
    %% ---------------------------------------------------------
    subgraph ReadBlock["Read Side (Read Service)"]
        Consumer["📥 Kafka Consumer"]
        Mongo[("🍃 MongoDB (Read Model)")]
        Redis[("⚡ Redis Cache")]
    end

    %% ---------------------------------------------------------
    %% FLOW CONNECTIONS
    %% ---------------------------------------------------------
    
    %% 1. User Request
    User -->|POST /reservations| Gateway
    Gateway --> LoadBalancer
    LoadBalancer --> API
    
    %% 2. Transactional Write
    API --> Step1
    Step1 --> Step2
    Step2 -->|Commit| SQL
    
    %% 3. Polling & Processing
    Poller -->|Poll READPAST| SQL
    SQL -->|Batch of Messages| Poller
    Poller -->|Push| Channel
    Channel -->|Pop| Worker
    
    %% 4. Dual Write (Projection)
    Worker -->|Project Data| MasterDB
    Worker -->|Publish Event| Kafka
    
    %% 5. Cleanup
    Worker -.->|Mark Processed| SQL
    
    %% 6. Read Side Update
    Kafka -->|Consume Event| Consumer
    Consumer -->|Update View| Mongo
    Mongo -.->|Invalidate/Update| Redis
    
    %% Styling
    style User fill:#fff,stroke:#333,stroke-width:2px
    style SQL fill:#bbdefb,stroke:#1565c0
    style MasterDB fill:#e1bee7,stroke:#7b1fa2
    style Mongo fill:#c8e6c9,stroke:#2e7d32
    style Kafka fill:#ffe0b2,stroke:#ef6c00
    style Channel fill:#fff9c4,stroke:#fbc02d
```

### Data Flow Stages

#### 1️⃣ **Command Processing (Write Side)**
- User sends `POST /reservations` to API Gateway
- Load Balancer routes to available API node
- API executes **atomic transaction**:
  - Writes reservation to `Reservations` table
  - Writes outbox message to `OutboxMessages` table
- Both succeed or both fail (ACID guarantee)

#### 2️⃣ **Async Event Processing (Master Node)**
- **Poller Thread**: Polls outbox using `WITH (READPAST)` hint
  - Avoids blocking locked rows
  - Fetches batch of unprocessed messages
- **Memory Channel**: Thread-safe queue for message batching
- **Worker Thread**: Processes messages using `Parallel.ForEachAsync`
  - Projects data to Master Reporting DB
  - Publishes events to Kafka
  - Marks messages as processed

#### 3️⃣ **Event Streaming**
- Domain events flow through Kafka topic: `cinema.reservations`
- At-least-once delivery guarantee
- Multiple consumers can subscribe

#### 4️⃣ **Read Model Update**
- **Kafka Consumer** receives events
- Updates denormalized MongoDB view
- Invalidates/updates Redis cache
- Read model eventually consistent with write model

#### 5️⃣ **Query Processing**
- User sends `GET` request via gRPC
- Read Service checks Redis cache first
- On cache miss, queries MongoDB
- Returns optimized denormalized view

## 🎯 Domain-Driven Design (DDD)

The system is organized around **bounded contexts** with clear domain boundaries and aggregate roots that maintain consistency. The **Saga pattern** coordinates cross-aggregate transactions for complex workflows like ticket purchasing.

```mermaid
graph TD
    %% ---------------------------------------------------------
    %% BOUNDED CONTEXT: RESERVATION (Core Domain)
    %% ---------------------------------------------------------
    subgraph "Reservation Context (Core Domain)"
        direction TB
        
        subgraph "Reservation Aggregate"
            Reservation[("Reservation<br/>(Aggregate Root)")]
            ReservationSeat["ReservationSeat<br/>(Entity)"]
            ReservationStatus["ReservationStatus<br/>(Value Object)"]
            
            Reservation -->|Contains| ReservationSeat
            Reservation -->|Has| ReservationStatus
        end
        
        subgraph "Domain Events"
            EvtResCreated["⚡ ReservationCreated"]
            EvtResConfirmed["⚡ ReservationConfirmed"]
            EvtResCancelled["⚡ ReservationCancelled"]
        end
        
        Reservation -.->|Emits| EvtResCreated
        Reservation -.->|Emits| EvtResConfirmed
    end

    %% ---------------------------------------------------------
    %% BOUNDED CONTEXT: SHOWTIME (Supporting Domain)
    %% ---------------------------------------------------------
    subgraph "Showtime Context (Supporting Domain)"
        direction TB
        
        subgraph "Showtime Aggregate"
            Showtime[("Showtime<br/>(Aggregate Root)")]
            MovieId["MovieId<br/>(Value Object)"]
            AuditoriumId["AuditoriumId<br/>(Value Object)"]
            ScreeningTime["ScreeningTime<br/>(Value Object)"]
            
            Showtime -->|Has| MovieId
            Showtime -->|Has| AuditoriumId
            Showtime -->|Has| ScreeningTime
        end
        
        subgraph "Showtime Events"
            EvtShowCreated["⚡ ShowtimeCreated"]
        end
        
        Showtime -.->|Emits| EvtShowCreated
    end

    %% ---------------------------------------------------------
    %% BOUNDED CONTEXT: TICKET PURCHASE (Saga Orchestration)
    %% ---------------------------------------------------------
    subgraph "Ticket Purchase Context (Saga)"
        direction TB
        
        subgraph "Saga Aggregate"
            TicketPurchaseSaga[("TicketPurchaseSaga<br/>(Aggregate Root)")]
            SagaState["SagaState<br/>(Value Object)"]
            SagaStep["SagaStep<br/>(Entity)"]
            
            TicketPurchaseSaga -->|Has| SagaState
            TicketPurchaseSaga -->|Contains| SagaStep
        end
        
        subgraph "Saga Events"
            EvtSagaStarted["⚡ SagaStarted"]
            EvtStepCompleted["⚡ StepCompleted"]
            EvtSagaCompleted["⚡ SagaCompleted"]
            EvtSagaCompensated["⚡ SagaCompensated"]
        end
        
        TicketPurchaseSaga -.->|Emits| EvtSagaStarted
        TicketPurchaseSaga -.->|Emits| EvtStepCompleted
        TicketPurchaseSaga -.->|Emits| EvtSagaCompleted
    end

    %% ---------------------------------------------------------
    %% BOUNDED CONTEXT: PAYMENT (Supporting Domain)
    %% ---------------------------------------------------------
    subgraph "Payment Context (Supporting Domain)"
        direction TB
        
        subgraph "Payment Aggregate"
            Payment[("Payment<br/>(Aggregate Root)")]
            PaymentStatus["PaymentStatus<br/>(Value Object)"]
            PaymentMethod["PaymentMethod<br/>(Value Object)"]
            
            Payment -->|Has| PaymentStatus
            Payment -->|Has| PaymentMethod
        end
        
        subgraph "Payment Events"
            EvtPaymentProcessed["⚡ PaymentProcessed"]
            EvtPaymentRefunded["⚡ PaymentRefunded"]
        end
        
        Payment -.->|Emits| EvtPaymentProcessed
        Payment -.->|Emits| EvtPaymentRefunded
    end

    %% ---------------------------------------------------------
    %% BOUNDED CONTEXT: TICKET (Supporting Domain)
    %% ---------------------------------------------------------
    subgraph "Ticket Context (Supporting Domain)"
        direction TB
        
        subgraph "Ticket Aggregate"
            Ticket[("Ticket<br/>(Aggregate Root)")]
            TicketStatus["TicketStatus<br/>(Value Object)"]
            TicketNumber["TicketNumber<br/>(Value Object)"]
            
            Ticket -->|Has| TicketStatus
            Ticket -->|Has| TicketNumber
        end
        
        subgraph "Ticket Events"
            EvtTicketIssued["⚡ TicketIssued"]
            EvtTicketCancelled["⚡ TicketCancelled"]
        end
        
        Ticket -.->|Emits| EvtTicketIssued
    end

    %% ---------------------------------------------------------
    %% SAGA ORCHESTRATION RELATIONSHIPS
    %% ---------------------------------------------------------
    TicketPurchaseSaga -->|1. Reserves| Reservation
    TicketPurchaseSaga -->|2. Charges| Payment
    TicketPurchaseSaga -->|3. Confirms| Reservation
    TicketPurchaseSaga -->|4. Issues| Ticket

    %% ---------------------------------------------------------
    %% INFRASTRUCTURE & INTEGRATION
    %% ---------------------------------------------------------
    subgraph "Infrastructure Layer"
        OutboxTable[("OutboxMessages Table")]
        KafkaBus["Kafka Event Bus"]
        
        EvtResCreated -->|Persisted to| OutboxTable
        EvtResConfirmed -->|Persisted to| OutboxTable
        EvtShowCreated -->|Persisted to| OutboxTable
        EvtSagaCompleted -->|Persisted to| OutboxTable
        EvtPaymentProcessed -->|Persisted to| OutboxTable
        EvtTicketIssued -->|Persisted to| OutboxTable
        
        OutboxTable -->|Polled by Master Node| KafkaBus
    end

    %% ---------------------------------------------------------
    %% READ MODELS (CQRS)
    %% ---------------------------------------------------------
    subgraph "Read Models (CQRS)"
        MongoView["MongoDB View<br/>(Denormalized)"]
        RedisCache["Redis Cache"]
        
        KafkaBus -->|Consumed by Read Service| MongoView
        MongoView -.->|Cached in| RedisCache
    end

    %% Relationships
    Reservation -->|References| Showtime
    Ticket -->|References| Reservation
    Ticket -->|References| Payment
    
    %% Styling
    style Reservation fill:#ffcc99,stroke:#cc6600,stroke-width:2px
    style Showtime fill:#99ccff,stroke:#0066cc,stroke-width:2px
    style TicketPurchaseSaga fill:#ffccff,stroke:#660066,stroke-width:3px
    style Payment fill:#c8e6c9,stroke:#2e7d32,stroke-width:2px
    style Ticket fill:#fff9c4,stroke:#f9a825,stroke-width:2px
    style OutboxTable fill:#e1f5fe,stroke:#0277bd
    style KafkaBus fill:#fff3e0,stroke:#ef6c00
    style MongoView fill:#c8e6c9,stroke:#2e7d32
```

### Bounded Contexts

#### 🎫 Reservation Context (Core Domain)
The heart of the business - manages seat reservations with strict consistency rules.

- **Aggregate Root**: `Reservation`
  - Enforces business rules (seat availability, time limits)
  - Contains `ReservationSeat` entities
  - Uses `ReservationStatus` value object (Pending, Confirmed, Cancelled)
- **Domain Events**: `ReservationCreated`, `ReservationConfirmed`, `ReservationCancelled`
- **Invariants**: No double-booking, reservation timeout enforcement

#### 🎬 Showtime Context (Supporting Domain)
Manages movie screening schedules and auditorium assignments.

- **Aggregate Root**: `Showtime`
  - References `MovieId`, `AuditoriumId` (value objects)
  - Manages `ScreeningTime` scheduling
- **Domain Events**: `ShowtimeCreated`
- **Invariants**: No overlapping showtimes in same auditorium

#### 🎭 Ticket Purchase Context (Saga Orchestration)
Coordinates the complete ticket purchase workflow across multiple bounded contexts using the Saga pattern.

- **Aggregate Root**: `TicketPurchaseSaga`
  - Orchestrates multi-step distributed transaction
  - Tracks progress via `SagaState` value object (Started, InProgress, Completed, Compensating, Failed)
  - Contains `SagaStep` entities representing each workflow step
- **Domain Events**: `SagaStarted`, `StepCompleted`, `SagaCompleted`, `SagaCompensated`
- **Invariants**: Steps execute in order, compensation reverses completed steps on failure
- **Orchestrated Steps**:
  1. Reserve Seats (Reservation Context)
  2. Process Payment (Payment Context)
  3. Confirm Reservation (Reservation Context)
  4. Issue Ticket (Ticket Context)

#### 💳 Payment Context (Supporting Domain)
Handles payment processing and refunds.

- **Aggregate Root**: `Payment`
  - Manages payment lifecycle
  - Uses `PaymentStatus` value object (Pending, Processing, Completed, Declined, Refunded, Failed)
  - Uses `PaymentMethod` value object (CreditCard, DebitCard, PayPal, ApplePay, GooglePay, BankTransfer)
  - Uses `Money` value object for amounts with currency support
- **Domain Events**: `PaymentCreated`, `PaymentCompleted`, `PaymentDeclined`, `PaymentRefunded`
- **Invariants**: One payment per reservation, refund only for completed payments

#### 🎫 Ticket Context (Supporting Domain)
Manages issued tickets and their lifecycle.

- **Aggregate Root**: `Ticket`
  - Represents a purchased ticket
  - Uses `TicketStatus` value object (Issued, Used, Voided)
  - Uses `TicketNumber` value object for unique identification
- **Domain Events**: `TicketIssued`, `TicketUsed`, `TicketCancelled`, `TicketRefunded`
- **Invariants**: Ticket requires confirmed reservation and completed payment

### DDD Patterns Applied

- **Aggregates**: Transactional consistency boundaries
- **Value Objects**: Immutable domain concepts (IDs, Status, Time, Money)
- **Domain Events**: First-class business occurrences
- **Repositories**: Aggregate persistence abstraction
- **Saga Pattern**: Long-running transaction coordination across aggregates
- **Compensation**: Rollback mechanism for saga failures
- **Result Pattern**: Explicit error handling without exceptions
- **Ubiquitous Language**: Business terms in code

## ✨ Key Features

### 🎯 Architectural Patterns
- **CQRS**: Separate read and write models for optimal performance
- **DDD**: Domain-driven design with bounded contexts and aggregates
- **Transactional Outbox**: Guarantees event delivery without distributed transactions
- **Saga Orchestration**: Manages distributed transactions with automatic compensation on failures
- **Event Sourcing**: Domain events captured and streamed via Kafka
- **Eventual Consistency**: Read models updated asynchronously

### 🚀 Technical Highlights
- **Load Balanced Write Side**: Horizontal scaling with multiple API nodes
- **READPAST Locking**: Concurrent outbox processing without blocking
- **TPL Batching**: `Parallel.ForEachAsync` for high-throughput event processing
- **Memory Channel**: Producer-consumer pattern for efficient message handling
- **Saga State Machine**: Step-by-step workflow with rollback capabilities
- **Compensation Transactions**: Automatic rollback of completed steps on failure
- **MongoDB Replica Set**: High availability for read operations
- **Redis Caching**: Sub-millisecond query response times
- **gRPC**: High-performance query service

## 🛠️ Technology Stack

| Component | Technology |
|-----------|-----------|
| **API Gateway** | YARP / Ocelot |
| **Write Database** | SQL Server |
| **Read Database** | MongoDB (Replica Set) |
| **Cache** | Redis |
| **Message Broker** | Apache Kafka |
| **API Framework** | ASP.NET Core |
| **Query Protocol** | gRPC |
| **Background Workers** | .NET Hosted Services |
| **Async Processing** | System.Threading.Channels |
| **Saga Orchestration** | Custom Saga State Machine |

## 📦 Components

### Write Side (Command)
- **API Gateway** (Port 5005): Entry point for all requests
- **Load Balancer** (Port 5003): Distributes traffic across API nodes
- **API Nodes** (Ports 5001-5002): Handle commands and write to SQL Server
- **SQL Server**: Transactional write database with Outbox table

### The Bridge (Master Node)
- **Poller Thread**: Continuously polls outbox using `READPAST` hint
- **Memory Channel**: Thread-safe queue for message batching
- **Worker Thread**: Background worker that:
  1. Processes messages in parallel using TPL
  2. Projects data to reporting database
  3. Publishes events to Kafka
  4. Marks messages as processed
- **Master SQL Server**: Centralized reporting database

### Event Streaming
- **Kafka Broker** (Port 9092): Event streaming platform
- **Topic**: `cinema.domain.events` for all domain events

### Read Side (Query)
- **Read Service**: Kafka consumer updating MongoDB views
- **MongoDB Replica Set**: 
  - 1 Primary (writes)
  - 2 Secondaries (read scaling)
- **Redis**: Query result caching
- **gRPC**: High-performance query API

### Saga Orchestrator (Distributed Transactions)
- **Ticket Purchase Saga**: Orchestrates multi-step ticket purchase workflow
- **State Machine**: Tracks saga progress through defined steps
- **Compensation Handler**: Automatically triggers rollback actions on failure
- **Steps**:
  1. Reserve Seats → Compensation: Release Seats
  2. Process Payment → Compensation: Refund Payment
  3. Confirm Reservation → Compensation: Cancel Reservation
  4. Issue Ticket → Compensation: Void Ticket

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Docker & Docker Compose
- SQL Server
- MongoDB
- Redis
- Apache Kafka

---

## 🔄 Saga Pattern - Ticket Purchase Orchestration

The ticket purchase flow uses the **Saga Orchestration Pattern** to coordinate a distributed transaction across multiple steps. If any step fails, compensation actions are executed in reverse order to maintain data consistency.

```mermaid
sequenceDiagram
    autonumber
    participant User as 👤 User
    participant API as 🌐 API Gateway
    participant Saga as 🎭 Saga Orchestrator
    participant Reservation as 📋 Reservation Service
    participant Payment as 💳 Payment Service
    participant Ticket as 🎫 Ticket Service

    User->>API: POST /api/ticketpurchase
    API->>Saga: Start Saga
    
    Note over Saga: Step 1: Reserve Seats
    Saga->>Reservation: ReserveSeats()
    Reservation-->>Saga: ✅ Seats Reserved
    
    Note over Saga: Step 2: Process Payment
    Saga->>Payment: ProcessPayment()
    Payment-->>Saga: ✅ Payment Processed
    
    Note over Saga: Step 3: Confirm Reservation
    Saga->>Reservation: ConfirmReservation()
    Reservation-->>Saga: ✅ Reservation Confirmed
    
    Note over Saga: Step 4: Issue Ticket
    Saga->>Ticket: IssueTicket()
    Ticket-->>Saga: ✅ Ticket Issued
    
    Saga-->>API: Saga Completed
    API-->>User: 200 OK (Ticket Details)
```

### Saga Compensation Flow (On Failure)

When a step fails, the saga automatically triggers compensation actions for all previously completed steps:

```mermaid
sequenceDiagram
    autonumber
    participant User as 👤 User
    participant API as 🌐 API Gateway
    participant Saga as 🎭 Saga Orchestrator
    participant Reservation as 📋 Reservation Service
    participant Payment as 💳 Payment Service

    User->>API: POST /api/ticketpurchase
    API->>Saga: Start Saga
    
    Note over Saga: Step 1: Reserve Seats
    Saga->>Reservation: ReserveSeats()
    Reservation-->>Saga: ✅ Seats Reserved
    
    Note over Saga: Step 2: Process Payment
    Saga->>Payment: ProcessPayment()
    Payment-->>Saga: ❌ Payment Failed (Insufficient Funds)
    
    Note over Saga,Reservation: 🔄 COMPENSATION PHASE
    
    Note over Saga: Compensate Step 1
    Saga->>Reservation: ReleaseSeats()
    Reservation-->>Saga: ✅ Seats Released
    
    Saga-->>API: Saga Failed (Compensated)
    API-->>User: 400 Bad Request (Error Details)
```

### Saga Steps Overview

| Step | Action | Compensation | Description |
|------|--------|--------------|-------------|
| 1 | `ReserveSeats` | `ReleaseSeats` | Temporarily reserve selected seats |
| 2 | `ProcessPayment` | `RefundPayment` | Charge customer's payment method |
| 3 | `ConfirmReservation` | `CancelReservation` | Permanently confirm the reservation |
| 4 | `IssueTicket` | `CancelTicket` | Generate and issue the ticket |

---

### Ticket Purchase API (Saga)

#### Purchase Ticket

```http
POST /api/ticketpurchase
Content-Type: application/json

{
  "showtimeId": "34306464-2135-4992-89b1-3e25839fbc4f",
  "customerId": "customer-123",
  "seats": [
    { "row": 5, "number": 10 },
    { "row": 5, "number": 11 }
  ],
  "paymentMethod": "CreditCard",
  "cardNumber": "4111111111111111",
  "cardHolderName": "John Doe"
}
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "ticketId": "ticket-abc123",
  "ticketNumber": "TKT-20250108123456-1234",
  "reservationId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
  "paymentId": "pay-xyz789",
  "movieTitle": "Inception",
  "screeningTime": "2025-12-12T20:00:00Z",
  "seats": [
    { "row": 5, "number": 10 },
    { "row": 5, "number": 11 }
  ],
  "totalPrice": 25.00
}
```

**Failure Response (400 Bad Request):**
```json
{
  "success": false,
  "error": "Compensated: Payment declined - insufficient funds",
  "sagaId": "saga-123456"
}
```

#### Get Saga Status

```http
GET /api/ticketpurchase/{sagaId}/status
```

**Response (200 OK):**
```json
{
  "sagaId": "saga-123456",
  "status": "Completed",
  "currentStep": 4,
  "totalSteps": 4,
  "failureReason": null,
  "createdAt": "2025-01-08T12:00:00Z",
  "completedAt": "2025-01-08T12:00:05Z",
  "ticketId": "ticket-abc123",
  "ticketNumber": "TKT-20250108123456-1234",
  "stepLogs": [
    {
      "stepName": "ReserveSeats",
      "success": true,
      "message": "Reserved 2 seats",
      "timestamp": "2025-01-08T12:00:01Z"
    },
    {
      "stepName": "ProcessPayment",
      "success": true,
      "message": "Payment processed, TransactionId: TXN-abc123",
      "timestamp": "2025-01-08T12:00:02Z"
    },
    {
      "stepName": "ConfirmReservation",
      "success": true,
      "message": "Reservation confirmed",
      "timestamp": "2025-01-08T12:00:03Z"
    },
    {
      "stepName": "IssueTicket",
      "success": true,
      "message": "Ticket issued: TKT-20250108123456-1234",
      "timestamp": "2025-01-08T12:00:04Z"
    }
  ]
}
```

---

### Payments API

#### Get Payment by ID

```http
GET /api/payments/{id}
```

**Response (200 OK):**
```json
{
  "id": "pay-xyz789",
  "reservationId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
  "customerId": "customer-123",
  "amount": 25.00,
  "currency": "USD",
  "status": "Completed",
  "method": "CreditCard",
  "transactionId": "TXN-abc123",
  "processedAt": "2025-01-08T12:00:02Z"
}
```

#### Refund Payment

```http
POST /api/payments/{id}/refund
Content-Type: application/json

{
  "reason": "Customer request"
}
```

**Response (200 OK):**
```json
{
  "id": "pay-xyz789",
  "status": "Refunded",
  "refundedAmount": 25.00,
  "refundReason": "Customer request"
}
```

---

### Tickets API

#### Get Ticket by ID

```http
GET /api/tickets/{id}
```

**Response (200 OK):**
```json
{
  "id": "ticket-abc123",
  "ticketNumber": "TKT-20250108123456-1234",
  "reservationId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
  "paymentId": "pay-xyz789",
  "showtimeId": "34306464-2135-4992-89b1-3e25839fbc4f",
  "customerId": "customer-123",
  "movieTitle": "Inception",
  "screeningTime": "2025-12-12T20:00:00Z",
  "auditoriumName": "Hall 1",
  "seats": [
    { "row": 5, "number": 10 },
    { "row": 5, "number": 11 }
  ],
  "totalPrice": 25.00,
  "status": "Issued",
  "qrCode": "QR:ticket-abc123:TKT-20250108123456-1234"
}
```

#### Get Ticket by Number

```http
GET /api/tickets/by-number/{ticketNumber}
```

#### Validate Ticket

```http
POST /api/tickets/{id}/validate
```

**Response (200 OK):**
```json
{
  "valid": true,
  "ticketNumber": "TKT-20250108123456-1234",
  "movieTitle": "Inception",
  "screeningTime": "2025-12-12T20:00:00Z",
  "seats": "Row 5, Seats 10-11"
}
```

#### Use Ticket (Mark as Used)

```http
POST /api/tickets/{id}/use
```

**Response (200 OK):**
```json
{
  "id": "ticket-abc123",
  "status": "Used",
  "usedAt": "2025-12-12T19:55:00Z"
}
```

---

### Health Check API

```http
GET /health
```

**Response (200 OK):**
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "kafka": "Healthy",
    "redis": "Healthy"
  }
}
```

---

## 🧪 Testing

* Unit Tests: xUnit
* Assertions: FluentAssertions
* Integration Tests: Dockerized test environment

---

## 🚀 Getting Started

### Prerequisites

- Docker & Docker Compose
- .NET 8 SDK

### Start Infrastructure

```bash
# Start all services
docker-compose up -d

# Check status
docker-compose ps
```

### Run API

```bash
cd src/Cinema.API
dotnet run
```

### Available Services

| Service | URL |
|---------|-----|
| Cinema API | http://localhost:5001 |
| Swagger UI | http://localhost:5001/swagger |
| Kafka UI | http://localhost:8080 |
| SQL Server | localhost:1433 |
| MongoDB Primary | localhost:27017 |
| Redis | localhost:6379 |

---

