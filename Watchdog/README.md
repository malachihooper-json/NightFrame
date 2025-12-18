# Watchdog

## Process Supervision and Reliability

The Watchdog service is a high-availability supervisor responsible for monitoring the health of NIGHTFRAME nodes.

### Responsibilities

- **Liveness Monitoring**: Periodically verifies the responsiveness of core services.
- **Automated Recovery**: Restarts failed processes and manages safe shutdown sequences.
- **Hot-swap Deployment**: Facilitates node updates by managing process state transitions.
- **System Resource Monitoring**: Tracks CPU and memory consumption to ensure node stability.
