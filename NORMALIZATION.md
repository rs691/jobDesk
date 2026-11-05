# Normalization with JobDesk's data

The live version of this lesson is at **`/Learn/Normalization`** in the app. Every table there is built from the database. This file is the same lesson for reading on paper.

## Before: one spreadsheet row per work order (unnormalized)

| WO | Title | Customer | Phone | City | Materials |
|---|---|---|---|---|---|
| 1 | Replace 120 ft privacy fence | Maple Street HOA | 555-0101 | Springfield | Wood picket 6 ft x200 @ $3.25; 4x4 post 8 ft x16 @ $12.50; Concrete mix 50 lb x32 @ $6.75 |
| 2 | Gate latch repair | Maple Street HOA | 555-0101 | Springfield | |
| 3 | Chain link along playground | Riverside Elementary | 555-0142 | Riverton | Chain link 50 ft x3 @ $89.00; 4x4 post 8 ft x10 @ $12.50; Concrete mix 50 lb x20 @ $6.75 |

**Problem:** the *Materials* cell holds a list. You can't sum quantities, filter "jobs that use concrete", or join on it without parsing text.

## 1NF: atomic values, no repeating groups

Split the list so each material line is its own row. The key becomes **(WorkOrderId, Material)**.

| WO | Title | Customer | Phone | City | Material | UnitCost | Qty |
|---|---|---|---|---|---|---|---|
| 1 | Replace 120 ft privacy fence | Maple Street HOA | 555-0101 | Springfield | Wood picket 6 ft | 3.25 | 200 |
| 1 | Replace 120 ft privacy fence | **Maple Street HOA** | **555-0101** | **Springfield** | 4x4 post 8 ft | 12.50 | 16 |
| 1 | Replace 120 ft privacy fence | **Maple Street HOA** | **555-0101** | **Springfield** | Concrete mix 50 lb | 6.75 | 32 |
| 2 | Gate latch repair | **Maple Street HOA** | **555-0101** | **Springfield** | | | |
| 3 | Chain link along playground | Riverside Elementary | 555-0142 | Riverton | Chain link 50 ft | 89.00 | 3 |
| 3 | Chain link along playground | **Riverside Elementary** | **555-0142** | **Riverton** | **4x4 post 8 ft** | **12.50** | 10 |
| 3 | Chain link along playground | **Riverside Elementary** | **555-0142** | **Riverton** | **Concrete mix 50 lb** | **6.75** | 20 |

**New problem:** bold cells are copies. Maple Street HOA's phone is stored 4 times. These are the three anomalies:

- **Update anomaly:** a phone change means updating 4 rows. Miss one and the data contradicts itself.
- **Insert anomaly:** Oak Ridge Church can't be added until it has a job, because there's no row to put it on.
- **Delete anomaly:** delete Maple's last job and Maple's phone number is gone too.

## 2NF: no partial dependencies

Rule: be in 1NF, and every non-key column depends on the **whole** key. With the key **(WorkOrderId, MaterialId)**:

| Column | Depends on | Moves to |
|---|---|---|
| Title, Status, Customer columns | WorkOrderId only | **WorkOrders** |
| MaterialName, UnitCost | MaterialId only | **Materials** |
| Quantity | both | stays in **WorkOrderMaterials** (junction table) |

Now a price change is one row in `Materials`.

## 3NF: no transitive dependencies

Rule: be in 2NF, and non-key columns depend on the key and **nothing but** the key.

`WorkOrders` would still hold CustomerName, Phone and City. Those depend on the customer, not the work order: `WorkOrderId → CustomerId → Phone`. Move them to **Customers** and keep only the foreign key `WorkOrders.CustomerId`.

## After: the real JobDesk schema

```
Customers (Id PK, Name, Phone, Email, City)
    1 ──< many
WorkOrders (Id PK, CustomerId FK → Customers, Title, Status, ScheduledDate)
    1 ──< many
WorkOrderMaterials (Id PK, WorkOrderId FK → WorkOrders, MaterialId FK → Materials, Quantity)
    many >── 1
Materials (Id PK, Name, Unit, UnitCost)

WorkOrderAttachments (Id PK, WorkOrderId FK → WorkOrders, FileName, StorageKey, ...)   -- see the AWS section of README
```

- **Junction table:** a work order uses many materials, and a material is used on many work orders. That's many-to-many, so it needs `WorkOrderMaterials`. The table also holds the one fact that belongs to the pair: `Quantity`. Its natural key is (WorkOrderId, MaterialId). It also has a surrogate `Id`, which is a common EF convention.
- **Foreign keys** stop orphans: you can't add a line for a work order that doesn't exist. Deleting a customer who still has work orders is blocked (`OnDelete(DeleteBehavior.Restrict)` in `JobDeskContext`).
- **The cost of normalizing is joins.** Reports have to join the tables back together, and that's exactly where the join-grain bug in [BUG.md](BUG.md) came from.
- **Deliberate denormalization:** real quoting systems often copy `UnitCost` onto the line when a job is quoted, so later price changes don't rewrite old invoices. That's a conscious trade-off for history, not an accident. JobDesk doesn't do this, to keep the lesson clean.

## 30-second answer

> "1NF: one value per cell, so material lines become rows. 2NF: nothing depends on only part of the key, so material name and price move to Materials, and title and customer move to WorkOrders. 3NF: nothing depends on another non-key column, so the customer's phone lives in Customers and WorkOrders just has CustomerId. The win is that each fact is stored once. The cost is joins, and that's where my report bug came from."
