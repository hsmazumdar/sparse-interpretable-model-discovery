# Experiment 23 protocol

Source: NASA Planetary Fact Sheet (metric), Moon excluded.
https://nssdc.gsfc.nasa.gov/planetary/factsheet/

a_AU = distance_1e6km / 149.6, P_year = period_days / 365.2.
Target law P = a^(3/2). Twenty seeds, RandomState(1000+seed),
permutation holdout of 2 test, 1 validation, 6 train.
Power recovery on a fixed grid a in logspace(0.3, 45, 40),
not on the two held-out planets alone.
