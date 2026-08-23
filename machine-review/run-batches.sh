#!/usr/bin/env bash
# Machine census labeling driver: 14 batches over 2 concurrent agy lanes.
cd "$(dirname "$0")/.." || exit 1
CREW="$USERPROFILE/.crew/bin/crew.cmd"
: > machine-review/driver.log

run_batch() {
  local n="$1"
  local p="machine-review/prompt-batch-$n.txt"
  if "$CREW" review --workers google --prompt "$(cat "$p")" --pretty \
      > "machine-review/batch-$n/output.json" 2> "machine-review/batch-$n/output.err"; then
    echo "batch $n OK $(date -u +%FT%TZ)" >> machine-review/driver.log
  else
    echo "batch $n FAILED exit=$? $(date -u +%FT%TZ)" >> machine-review/driver.log
  fi
}

lane_a() { for n in 01 02 03 04 05 06 07; do run_batch "$n"; done; }
lane_b() { sleep 15; for n in 08 09 10 11 12 13 14; do run_batch "$n"; done; }

lane_a &
lane_b &
wait
echo "ALL DONE $(date -u +%FT%TZ)" >> machine-review/driver.log
