# Birth Quality Lifespan Fix

This repository contains the source code for the RimWorld mod **Birth Quality Lifespan Fix**.

https://steamcommunity.com/sharedfiles/filedetails/?id=3654799710

# Calculation Logic

To ensure compatibility with the vanilla **Birth Quality Curve** (which relies on specific human age milestones), this mod calculates a **Human Equivalent Age** for every pawn before applying the curve.

This calculation divides the pawn's life into three phases: **Growth**, **Prime Plateau**, and **Decline**.

### 1. The Ratios
First, we establish two scaling factors based on the race's stats relative to humans (Human Maturity: 18, Human Lifespan: 80).

$$R_{mat} = \frac{\text{Race Maturity}}{18}$$

$$R_{life} = \frac{\text{Race Lifespan}}{80}$$

### 2. The Breakpoints
We calculate the specific biological ages where this race should hit the start and end of the "Prime Birth Quality" window (which corresponds to Human ages 20 to 30).

* **Peak Start:** The biological age equivalent to Human 20.
    $$P_{start} = 20 \times R_{mat}$$

* **Peak End:** The biological age equivalent to Human 30.
    $$P_{end} = \max(30 \times R_{life}, P_{start})$$
    *(The max function acts as a safety lock to ensure the prime window never inverts for races with extremely long childhoods.)*

### 3. The Transformation
The equivalent age ($E$) is determined by which phase of life the pawn is currently in:

#### A. Growth Phase (Age $\le P_{start}$)
The pawn is still developing. We scale their age purely by the maturity ratio.
$$E = \frac{\text{BioAge}}{R_{mat}}$$

#### B. Decline Phase (Age $\ge P_{end}$)
The pawn is past their prime. We start from the Human baseline of 30 and add the scaled time that has passed since their peak ended.
$$E = 30 + \frac{\text{BioAge} - P_{end}}{R_{life}}$$

#### C. The Plateau ($P_{start} <$ Age $< P_{end}$)
The pawn is within their prime window. We linearly interpolate their position in this window to map it to the Human 20–30 range.
$$E = 20 + \left( \frac{\text{BioAge} - P_{start}}{P_{end} - P_{start}} \times 10 \right)$$

---

This work is licensed under a
[Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License][cc-by-nc-sa].

[![CC BY-NC-SA 4.0][cc-by-nc-sa-image]][cc-by-nc-sa]

[cc-by-nc-sa]: http://creativecommons.org/licenses/by-nc-sa/4.0/
[cc-by-nc-sa-image]: https://licensebuttons.net/l/by-nc-sa/4.0/88x31.png
[cc-by-nc-sa-shield]: https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg
