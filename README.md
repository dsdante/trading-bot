# A trading bot

A trading bot for the Tinkoff Invest broker.

A work in progress.

## 1. Prerequisites
* A Tinkoff Invest [account](https://tinkoff.ru/invest) and an [access token](https://tinkoff.github.io/investAPI/token)
* PostgreSQL
* Current user's right to create a database in it

## 2. Running locally

Create a `secrets.json` file in the directory `~/.microsoft/usersecrets/TradingBot` (on Linux) or `%APPDATA%\Microsoft\UserSecrets\TradingBot` (on Windows) with the following content:
```json
{
  "Database": {
    "Host": "localhost",
    "Username": "your_postgres_login",
    "Password": "your_postgres_password",
    "Database": "trading_bot"
  },

  "Tinkoff": {
    "AccessToken": "your_Tinkoff_Invest_access_token"
  }
}
```

Alternatively, you can use Unix domain socket (on Linux) or SSPI (on Windows) to connect to PostgreSQL locally without a password.

A similar file with a different `Database` should be added to `../TradingBotTest` for automated tests.

**Make sure your token doesn't get commited to a repository or get outside your computer in any other way.**

The main logic's entry point is in `Services/Worker.cs`.
