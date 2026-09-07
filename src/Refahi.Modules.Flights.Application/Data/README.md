# Airline code catalogue

`iata_airlines.csv` is a pinned snapshot of `generated/iata_airlines.csv` from
[`benct/iata-utils`](https://github.com/benct/iata-utils) at commit
`9d95673c23baaa318db2fa2c3088e82f87be3c53` (MIT). The upstream project states
that its airline reference data is derived from Open Travel Data (OPTD).

To refresh it, replace the file with the same artifact from a reviewed pinned
commit, retain the `^`-delimited header and columns, and run the Flights tests.
Provider-specific corrections belong in
`Flights:AirlineLogos:CodeOverrides`; do not edit runtime mappings ad hoc.
