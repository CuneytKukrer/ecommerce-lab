# ecommerce-lab

A distributed e-commerce system built to explore production-grade patterns 
that are easy to describe in interviews but hard to get right in practice: 
**saga compensation, idempotency, reconciliation after ambiguous failures, 
event-driven messaging, and distributed tracing.**

I built this to move beyond theory — every pattern here was tested against 
real failure scenarios (timeouts, partial failures, race conditions).

## Architecture
Client → API Gateway (YARP, rate limiting)  
├── Cart Service (Redis)  
├── Inventory Service (PostgreSQL, pessimistic locking)  
├── Order Service (PostgreSQL, saga orchestration)  
├── Payment Service (PostgreSQL, idempotency)  
└── Notification Service (RabbitMQ consumer)  

All services export traces via OpenTelemetry → Jaeger.  


![Jaeger trace](docs/images/jaeger-trace.png)  
*Full distributed trace: Gateway → Order → Cart, Inventory, Payment*

## Services

| Service | Responsibility | Tech |
|---|---|---|
| Cart | Session-based cart management | .NET 8, Redis (TTL 30d) |
| Inventory | Stock reservation, overselling prevention | .NET 8, PostgreSQL, `SELECT FOR UPDATE` |
| Order | Checkout, state machine, saga orchestration | .NET 8, PostgreSQL |
| Payment | Mock payment provider, idempotency | .NET 8, PostgreSQL, unique index on `idempotency_key` |
| Notification | Order event consumer | .NET 8, RabbitMQ, manual ack/nack |
| API Gateway | Routing, rate limiting (100 req/min/IP) | YARP, .NET 8 |
| Reconciliation Worker | Background job resolving ambiguous payments | .NET 8, `BackgroundService` |

## Key Patterns & Why They Matter

### Saga compensation
When payment fails after inventory has been reserved, the system must release 
the reservation — otherwise stock is locked up forever. Implemented as 
explicit compensating transactions in the checkout flow.

### Idempotency
A client retry after a network timeout must never result in a double charge. 
Every payment request carries an `Idempotency-Key`. The DB enforces uniqueness, 
and a race condition (two parallel requests) is handled via `ON CONFLICT DO NOTHING`.

### Reconciliation after ambiguous failures
The hardest case: a timeout *between* payment commit and response means the 
payment succeeded but the order doesn't know. A background worker periodically 
queries the payment service (read-only GET) and resolves the order state. 
This is the "unknown outcome" problem that production payment systems face daily.

### At-least-once messaging
RabbitMQ with manual ack/nack and persistent messages. The consumer is 
idempotent, so duplicate delivery is safe.

### Pessimistic locking
`SELECT ... FOR UPDATE` on inventory rows prevents overselling during concurrent 
checkout attempts.

### Distributed tracing
OpenTelemetry + Jaeger. Trace begins at the gateway and propagates through 
HttpClient (via W3C `traceparent`) and YARP. Used to identify bottlenecks — 
e.g., payment service accounts for ~80% of checkout latency.

## Trade-offs (what I chose, and why)

| Decision | Chosen | Alternative | Why |
|---|---|---|---|
| Checkout → Payment | Async (queue) | Sync | Payments can be slow; user shouldn't wait |
| Cart storage | Redis | PostgreSQL | TTL, session, speed |
| Order/Payment DB | PostgreSQL | MongoDB | ACID, idempotency constraints |
| Inventory locking | Pessimistic (`FOR UPDATE`) | Optimistic | Correctness over throughput for v1 |
| Reconciliation trigger | Background worker (5 min) | On-demand | Simple, self-healing |

## Running Locally

**Prerequisites:** Docker Desktop, .NET 8 SDK

```bash
# 1. Start infrastructure
docker compose up -d
# Redis :6379, PostgreSQL :5432, RabbitMQ :5672 (+ UI :15672), Jaeger :16686

# 2. Create databases
docker exec -it <postgres-container> psql -U dev -d postgres -c \
  "CREATE DATABASE inventory_db; CREATE DATABASE order_db; CREATE DATABASE payment_db;"

# 3. Start services (separate terminals)
dotnet run --project src/CartService          # :5001
dotnet run --project src/InventoryService     # :5002
dotnet run --project src/PaymentService       # :5003
dotnet run --project src/OrderService         # :5004
dotnet run --project src/NotificationService  # :5005
dotnet run --project src/ApiGateway           # :5000

# 4. Run a checkout (PowerShell)
curl.exe -X POST http://localhost:5000/api/cart/user1/items `
  -H "Content-Type: application/json" `
  -d "{\"productId\": 101, \"quantity\": 2}"

curl.exe -X POST http://localhost:5000/api/orders/checkout/user1
```

**Observe:** Open Jaeger at http://localhost:16686 → service ApiGateway → see the full trace.

### Testing
Unit + integration tests: dotnet test (xUnit)

Failure scenarios tested manually:

Concurrent checkout for last unit (overselling prevention)

Payment timeout → reconciliation → order resolved correctly

Duplicate Idempotency-Key → single payment row  

### Known Limitations & Next Steps
No Product Service yet — prices are stubbed at 0. Next: add a catalog service with read-replica.

Pessimistic locking only — flash-sale scenarios would benefit from Redis atomic decrement as a pre-filter.

Notification is log-based — next: real email/SMS integration.

Single region — no multi-region failover.  


### What I Learned
Building this taught me that "what happens on timeout" is where most distributed
system designs break down. Timeout is not "failed" — it's "unknown," and
handling that correctly requires idempotency, reconciliation, and a state machine
that can hold ambiguity. The README in [learning-journey/notes/distributed-systems.md](https://github.com/CuneytKukrer/learning-journey/blob/main/notes/distributed-systems.md)
has more detail on the critical window between commit and response.  
