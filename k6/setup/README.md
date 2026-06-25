# k6 Scenario Seed Data

`setup-scenario-data.sql` prepares repeatable fixture data for the k6 scenario tests.

What it does:

1. Builds an in-memory `@Fixtures` table with known test customers and accounts.
2. Deletes old `Transactions` that belong to those fixture accounts.
3. Inserts or updates `IndividualCustomers` records for k6 test users.
4. Inserts or updates `Accounts` records for those customers.
5. Prints a small summary of prepared accounts and affected rows.

Tables affected:

| Table | Action |
|---|---|
| `IndividualCustomers` | Inserts or updates k6 test customer records |
| `Accounts` | Inserts or updates active TRY accounts for those customers |
| `Transactions` | Deletes old transactions linked to fixture accounts; does not insert new transactions |

Scenario data:

| Scenario | Seed data |
|---|---|
| `overdraft-race` | 1 sender account with `100.00 TRY`, 20 receiver accounts with `0.00 TRY` |
| `hotspot-load` | 1 hot receiver account with `0.00 TRY`, 50 sender accounts with `100000.00 TRY` |
| `baseline-transfer-load` | 1 sender account with `1000000.00 TRY`, 1 receiver account with `0.00 TRY` |
| `spike-traffic` | 100 sender accounts with `1000000.00 TRY`, 100 receiver accounts with `0.00 TRY` |

In short: this SQL file resets known customers and accounts so race/load/spike tests can run repeatedly against predictable data.
