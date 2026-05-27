# Phase 3K - Listen Profiles

Phase 3K adds per-band Listen Profiles to the Windows Pluto SDR Scanner GUI.

## Why

NOAA 162.500 MHz testing showed that the original listen path was not a GUI frequency-unit problem. The Pluto was receiving RF correctly, but the audio backend needed complex FM channel filtering before the FM discriminator. After Phase 3H/3I tuning, tests 06 and 10 were the best current NFM/NOAA settings.

## GUI changes

A new **Listen Profile** drop-down is added near **Listen / Record Selected**:

- Auto by Mode
- NOAA/NFM Clean Normal
- NOAA/NFM Clean Invert-Q
- Ham FM NFM
- Airband AM
- Broadcast FM WBFM
- Digital Detect Only

## Current default behavior

`Auto by Mode` chooses:

- `nfm` -> `NOAA/NFM Clean Normal`
- `am` -> `Airband AM`
- `wbfm` -> `Broadcast FM WBFM`

Digital signals are detect/log only for now. Analog listen is disabled for the Digital Detect Only profile.

## Current best NOAA/NFM profiles

`NOAA/NFM Clean Normal` uses:

```text
--rx-channel 1
--iq-mode normal
--fm-channel-lowpass-hz 8000
--audio-lowpass-hz 3000
--audio-highpass-hz 150
--volume 0.45
--squelch-off
```

`NOAA/NFM Clean Invert-Q` uses the same settings except:

```text
--iq-mode invert-q
```

These correspond to the best-understandable Phase 3I test cases 06 and 10.

## Notes

This does not mean every band now has perfect audio. It gives us mode-specific defaults and a cleaner GUI wiring path. Airband AM, broadcast FM, and digital modes will still need their own validation and tuning.
