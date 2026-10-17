# ecommerce-lab
A distributed e-commerce system built to explore production-grade patterns: 
saga compensation, idempotency, reconciliation, event-driven messaging, and API gateway.

## Architecture
Client → API Gateway (YARP) → Cart Service (Redis)  
→ Inventory Service (PostgreSQL, pessimistic locking)  
→ Order Service (PostgreSQL, saga orchestration)  
→ Payment Service (PostgreSQL, idempotency)  
→ Notification Service (RabbitMQ consumer)  


## Services

| Service | Responsibility | Tech |
|---|---|---|
| Cart | Session-based cart management | .NET 8, Redis |
| Inventory | Stock reservation, overselling prevention | .NET 8, PostgreSQL, `SELECT FOR UPDATE` |
| Order | Checkout, state machine, compensation | .NET 8, PostgreSQL |
| Payment | Mock payment provider, idempotency | .NET 8, PostgreSQL |
| Notification | Order event consumer | .NET 8, RabbitMQ |
| API Gateway | Routing, rate limiting | YARP |

## Key Patterns Demonstrated

- **Saga compensation:** Failed payment releases inventory reservations.
- **Idempotency:** Same `Idempotency-Key` never charges twice.
- **Reconciliation:** Background worker resolves ambiguous payment outcomes after timeouts.
- **At-least-once messaging:** RabbitMQ with manual ack/nack.
- **Pessimistic locking:** `SELECT ... FOR UPDATE` prevents overselling during flash sales.

## Running Locally

```bash
docker compose up -d
# start each service in src/
dotnet run --project src/CartService
...

What I Learned
See learning-journey/notes/distributed-systems.md for detailed notes on
timeout windows, idempotency, and reconciliation.
