StarAllianceSearch
==================

Star Alliance search is a tool for searching for business award flights using the sas api.

Building
===============
* Initialize submodules
** git submodule init
** git submodule update
* Open solution in visual studio and build


Examples
===========

From ARN to LAX
_StarAllianceSearch -from ARN -to LAX -out 2019-12-01 -in 2020-01-12_

Multiple destinations, some banned carriers
_StarAllianceSearch -from ARN -to KIX,NRT -out 2019-12-01 -in 2020-01-12 -BannedCarriers CA,LO_

All options:
>STAR ALLIANCE BUSINESS CLASS SEARCH
Arguments: (default value in parethesis)
-From            (required) Originating airport (code).
-To              (required) Destination airport (code).
-Out             (required) The first day to start searching out trips from (format YYYY-MM-DD).
-In              (required) The first day to start searching return trips from (format YYYY-MM-DD).
-SearchSpan      (7)        Number of days to search in (each results in a new query).
-BannedCarriers             A comma separated list of carrier codes to filter out.
-MaxStops        (-1)       Maximum numbers of stops to allow for the trip (-1 = no limit).
-MaxTransitStops (0)        Maximum number of transit stops (stop without changing airplane). -1 = no limit).
-Config                     Config file to read options from. One option per line, format is argument=value. (Lines starting with # is ignored).
-TranslateCodes             Write a table after all trips with translations for all codes.
-Help                       Print this help message.