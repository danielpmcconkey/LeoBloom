-- MIGRONDI:NAME=1712000025000_AddIrregularCadence.sql
-- MIGRONDI:TIMESTAMP=1712000025000
-- ---------- MIGRONDI:UP ----------

UPDATE ops.obligation_agreement SET cadence = 'irregular' WHERE cadence = 'tri_annual';

-- ---------- MIGRONDI:DOWN ----------

UPDATE ops.obligation_agreement SET cadence = 'tri_annual' WHERE cadence = 'irregular';
