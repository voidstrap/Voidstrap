# Datacenters/ServerFetch.json

## What it is

A list of Roblox's own server IP ranges, all sitting inside 128.116.x.x, with each /24 chunk mapped to the city, region, country, and coordinates of the datacenter it actually runs out of. This is what the Voidstrap Matchmaker reads to figure out where a server really is before deciding whether to join it

## What it looks like

```json
{
  "Servers": {
    "<CIDR>": {
      "Cidr": "<CIDR>",
      "City": "string",
      "Region": "string",
      "Country": "string",
      "Lat": 0.0,
      "Lon": 0.0,
      "SeenCount": 0,
      "IPs": ["x.x.x.x"]
    }
  },
  "SchemaVersion": 1
}
```

## Fields

* `Cidr`, the /24 block itself, matches its own key under `Servers`
* `City`, `Region`, `Country`, the real world location that block maps to
* `Lat`, `Lon`, the coordinates used to measure distance from the player
* `SeenCount`, how many times a server inside that range has actually been seen and confirmed
* `IPs`, a few real addresses from that range, proof the mapping is correct and something concrete to ping for real latency
* `SchemaVersion`, the format version of the file itself, only bumped if the structure changes, not for normal additions

## How the Matchmaker uses it

1. Get a rough idea of where the player is
2. Look up the IP of the server they're about to join in this table
3. Compare that server's location to the player's
4. If it's far away, rejoin or hop to a different server and check again
5. Repeat until they land on something close by

## Notes

This is a snapshot, not a live feed, so it needs occasional updates as Roblox changes its ranges. Also, close on a map doesn't always mean low ping in practice, so the distance check is more of a fast filter than a guarantee.
