# Auto Routes for Voxel Tycoon

Auto Routes uses Voxel Tycoon's native saved routes as named vehicle groups. The in-game controls are in English; this guide is available in English and German.

## English

Three new buttons appear in the three-dot menu:

1. **Organize now** runs the grouping immediately.
2. **Toggle automatic mode** enables or disables continuous organization. Default: on.
3. **Protect/include manual routes** controls whether existing player-created routes may be changed. Default: protected.

Settings and the IDs of automatically managed routes are stored in the save game.

At least two vehicles are grouped when both their complete vehicle consist and their complete schedule match. The comparison includes vehicle order, orientation, stops, tasks, and travel direction. The mod uses no Harmony patches or native mod loader.

Route names follow the compact pattern `COA TRAIN MN↔KW A`:

- Cargo comes first for alphabetical sorting (`PAS` means passengers).
- The short vehicle class and route follow (`TRK`, `CAR`, `TRAIN`; German classes retain compact names such as `LKW` and `PKW`).
- The final `A` marks an automatically managed route.
- Stop abbreviations use two to four characters and remain unique across different stop names.
- Prefixes such as Bad, Saint/St., New, Old, Great, and Little receive special handling.
- Longer schedules show the first two and the final stop.
- Name collisions use the vehicle model and then a stable number.

## Deutsch

Auto Routes verwendet die normalen gespeicherten Routen von Voxel Tycoon als benannte Fahrzeuggruppen.

Im Menü mit den drei Punkten erscheinen drei neue Schaltflächen:

1. **Organize now** führt die Gruppierung sofort aus.
2. **Toggle automatic mode** schaltet die laufende Zuordnung um. Standard: an.
3. **Protect/include manual routes** bestimmt, ob vorhandene eigene Routen verändert werden dürfen. Standard: geschützt.

Alle Einstellungen und die Kennzeichnung der automatisch verwalteten Routen werden im Spielstand gespeichert.

Mindestens zwei Fahrzeuge werden zusammengefasst, wenn der komplette Fahrzeugverband und der komplette Fahrplan übereinstimmen. Berücksichtigt werden Reihenfolge, Ausrichtung, Haltepunkte, Aufgaben und Fahrtrichtung. Die Mod verwendet keine Harmony-Patches und keinen nativen Mod-Loader.

Die Namen folgen dem kompakten Muster `KOH ZUG MN↔KW A`:

- Die Fracht steht vorn, damit alphabetisch nach Fracht sortiert werden kann (`PAS` für Passagiere).
- Danach folgen die kurze Fahrzeugklasse (`LKW`, `PKW`, `ZUG`) und die Strecke.
- Das abschließende `A` kennzeichnet automatisch verwaltete Routen.
- Haltestellen erhalten eindeutige Kürzel mit zwei bis vier Zeichen.
- Vorsätze wie Bad, Sankt/St., Neu, Alt, Groß und Klein werden gesondert behandelt.
- Längere Fahrpläne zeigen die ersten beiden und den letzten Halt.
- Bei Namensüberschneidungen folgen Fahrzeugmodell und anschließend eine stabile Nummer.
