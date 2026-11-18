# oth-fuel-management

**FuelManagement**: an [Over the Hill](docs/games/over-the-hill/README.md) MelonLoader mod that gives
every car a fuel tank. Driving burns fuel (climbing burns more), any house refills it, side jerry
cans add range, and an empty tank puts the car into limp mode (a crawl, worse than maximum damage)
until you reach a house. In multiplayer, hold F11 next to another player's car to siphon their
fuel. Settings: the settings hub tab (F10 menu without the hub).

```bash
python3 -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
tools/check.sh                                # all checks, no deploy
cd FuelManagement && dotnet build -c Release  # build + deploy to the game
```

Details: [docs/project/README.md](docs/project/README.md). Agents: start at [AGENTS.md](AGENTS.md).
