I want a KSA game mod which is a mission mod.

- it should monitor game state passively all the time.  the rate at which it does this needs to be configurable so that it doesn't cause a burden (e.g. let the mod set it to run every N milliseconds)
- there should be a set of data that is being monitored, this is things like:
  - vehicle altitude (from its current soi body)
  - vehicle speed (in various frames of reference)
  - vehicle soi parent body
  - g-forces of vehicle
  - distance of vehicle to other vehicles under the same soi parent
  - apoasis distance
  - periapsis distance
  - vehicle mass
- it should be able to detect certain kinds of interesting events like
  - when a vehicles parent soi body changes
  - when a vehicle lifts off from ground
  - when a vehicle lands on ground
  - when a vehicle has left a given planets atmosphere
  - when a vehicle has entered a given planets atmosphere


The point of passively monitoring this data is to be able to create a few things:

- a mission system which has predefined parameters to be met to complete the mission
- a passive system of sending player events to a global shared state system hosted on the internet to have a global feed of player activity of interesting events (so that I could e.g. know that some other person set a new speed record)

I want this to be done in a fairly robust manner as KSA is under heavy development and will be changing its code base alot over time.

The way we observe these metrics should be coded in an abstracted centralized way so that maintenance on how to obtain them is co-located and minimized to fix when KSA game code changes.