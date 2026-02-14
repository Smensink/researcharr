## 2024-07-25 - Local Development Environment Setup

**Learning:** The local development environment for this repository is not straightforward. The `dotnet` command is not available in the default environment, and the `README.md` and `AGENTS.md` files strongly indicate that the application should be run via Docker. However, the `docker compose` command can fail due to permissions and may time out. This makes it difficult to run the application for verification.

**Action:** In the future, I will immediately use `sudo docker compose up --build -d` to start the application. If it times out, I will check the container status with `sudo docker compose ps` before attempting to run any verification scripts. I will not attempt to run the application directly with `dotnet` or `yarn start`.
