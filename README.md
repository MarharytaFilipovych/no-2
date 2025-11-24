###### Assignment #3
## Clean Architecture Backend — **Auction Service**

### Goals
After the third workshop, the goals of this assignment are:

1. To apply Clean Architecture with clear separation between **Domain**, **Application**, and **Infrastructure** layers.
2. To design domain logic that involves state transitions, bidding rules, and winner selection that cannot be reduced to simple CRUD.
3. To enforce configuration-driven behavior (auction modes, tie-breaking policies) and delayed post-auction decision handling.

### Overview
This assignment focuses on implementing a backend-only system that manages multiple auctions with different visibility and resolution rules.

The system must support both **open** and **blind** auctions.  
In an open auction, participants may see the current highest bid while the auction is active.  
In a blind auction, bid amounts remain hidden until the auction ends, and participants see only general activity (e.g., bid count).

The result of each auction must be finalized based on configurable rules, including tie-breaking and payment-verification time windows.

### Task

#### 1. Implement the Auction Lifecycle
Each auction must follow the state sequence:

- **Pending** — created, configured, and scheduled to start in the future.
- **Active** — bidding is allowed!
- **Ended** — bidding is closed at the end time.
- **Finalized** — winner is confirmed or no-winner is declared.

State transitions must follow the rules:
- bidding is allowed only in Active,
- finalization is allowed only after Ended,
- finalization occurs once.

#### 2. Support Two Auction Types
- **Open Auction**
  - the highest bid amount is visible to participants,
  - users allowed to place as many bids as they want,
  - only valid bid higher than the current price + increment is accepted,
- **Blind Auction**
  - bid amounts remain hidden until the auction ends,
  - only one bid per user is allowed (previous bid being removed on new one),
  - all bids are accepted,
  - participants may query bid count but not values.

The type is selected by the admin at creation time and cannot change later.

#### 3. Auction Creation Parameters
Admin must provide the following parameters when creating an auction:

- **title** — required, non-empty name of the auctioned item,
- **description** — optional text,
- **startTime** — optional, future timestamp when auction becomes Active (start immediately if omitted),
- **endTime** — timestamp strictly after startTime (can be replaced with duration like 2h, 5min etc.),
- **auctionType** — `open` or `blind`,
- **minimumIncrement** — optional numeric value (only applies to open auctions),
- **minPrice** — numeric value, in case no bids meet this price, no winner is declared,
- **softCloseWindow** — duration before endTime during which new bids extend the auction by that duration (e.g., 5 minutes),
- **showMinPrice** — (for blind auctions) boolean flag indicating if minPrice is visible to participants,
- **tieBreakingPolicy** — one of:
  - `earliest`
  - `randomAmongEquals`.

All parameters are immutable after auction creation.

#### 4. Bidding and Validation Rules
Each bid must satisfy:
- auction is Active and current time is before endTime,
- bid amount is greater than the current leading bid + increment (for open auctions),
- bids CAN be withdrawn while auction is active, but not after the auction ends,
- there is a global coefficient limiting maximum bid amount (e.g., no bid can exceed 1,000,000 UAH),
- there is a global coefficient limiting bid/account balance ratio (e.g., bid cannot exceed 50% of participant's deposited funds, or 200% etc.).

#### 5. Winner Selection and Finalization
When an auction reaches Ended state:
- admin triggers finalization,
- the highest valid bid becomes the provisional winner,
- if multiple highest bids match, apply the configured tie-breaking policy,
- if the reservePrice (if set) is not met, no winner is assigned.

#### 6. Post-Auction Payment Window
After finalization:
- the provisional winner has **3 hours** (configurable) to provide sufficient funds to account,
- if the deadline passes and balance is insufficient:
  - the winner is rejected,
  - user is banned from participating in future auctions for N days,
  - the next eligible bid is promoted,
  - the process repeats until a valid winner is assigned or no bids remain.

Only one active winner may exist at any time.

### System Features

#### Mandatory Requirements
- create and schedule auctions (admin only),
- view active auctions,
- display auction details with visibility rules based on type,
- deposit funds into participant account,
- participants can view balance and bid history,
- participants can view auctions they are winning or have won,
- place bids with correct application and domain level validation,
- finalize auctions and determine outcomes,
- payment timeout handling and automatic winner replacement,
- persistence of auctions, bids, and participant balances.
- **soft-close** behavior (extend end time if bids arrive near deadline),


#### Optional (+1 point)
**No-repeat-winner policy**
Auctions share a category, and a participant who has already won in that category becomes ineligible for subsequent wins during the same cycle. Finalization must automatically skip the highest bid from an already-winning participant and promote the next valid bid while keeping tie-breaking and reserve rules intact.


### Testing
Unit tests must cover:
- bid validation rules,
- state transition constraints,
- winner selection logic,
- payment expiration and winner promotion,
- open vs blind visibility behavior at the domain boundary.

### Grading Policy
| Criterion | Points                     |
|-----------|----------------------------|
| Lifecycle + state transitions | 2                          |
| Bidding rules + validation | 2                          |
| Open and blind modes | 2                          |
| Finalization + tie-breaking | 1                          |
| Payment window + winner replacement | 2                          |
| **Total** | **9**                      |
| **+1 (bonus)** | Optional feature |

### Notes
- Keep **business rules inside the Domain layer**. Pay extra attention to separation of concerns and dividing what belongs to Application vs Domain.
- You have starter code with basic project structure and authorization, you can use it or skip.
- You can work with in-memory repos or add persistence as you see fit, but focus on domain logic first.
- Avoid placing logic in controllers or repositories!
- The system must remain extensible without modifying core domain logic.
