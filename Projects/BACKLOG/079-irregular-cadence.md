# 079 — Add `irregular` recurrence cadence

## Problem

Agreement 8 (HOA — Lockhart) has a tri-annual schedule that doesn't
fit any regular cadence: Feb 1, May 1, Aug 1. The value `tri_annual`
was stored in the DB but `RecurrenceCadence` in the F# domain doesn't
recognize it — `obligation agreement list` crashes with:

```
Corrupt cadence in DB: Invalid RecurrenceCadence: 'tri_annual'
```

## What to do

1. Add `irregular` as a valid `RecurrenceCadence` discriminated union
   case. It means "this agreement recurs, but not on a computable
   schedule." Instance spawning is manual — the Saturday procedure
   handles it based on the agreement's `notes` field.

2. The `spawn` command should refuse to auto-spawn for `irregular`
   agreements (or at minimum not crash). Irregular instances are
   created manually.

3. Migration to update agreement 8's cadence from `tri_annual` to
   `irregular`.

## Acceptance criteria

- `obligation agreement list` no longer crashes
- `obligation agreement show 8` displays cadence as `irregular`
- `obligation instance spawn` skips irregular agreements gracefully
- Existing tests still pass

## Notes

The agreement's `notes` field documents the actual schedule. Hobson's
Saturday procedure will check irregular agreements and spawn instances
as due dates approach.
