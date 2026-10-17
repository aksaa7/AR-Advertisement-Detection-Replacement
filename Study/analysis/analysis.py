"""
Analysis of the AR Advertisement Detection & Replacement user study.

Within-subjects design, N = 18, three conditions:
    A = Original advertisements
    B = Blurred advertisements
    C = Advertisements replaced by a relaxing video

Measures (per condition)
    SUS    System Usability Scale (0-100)
    TLX    NASA-TLX raw score (6 subscales, 0-20; "Performance" reversed)
    Trust  Trust in Automation (19 items, 1-5; 5 items reversed)
    ARI    Augmented Reality Immersion questionnaire (42 items, 1-7; 5 items reversed)
    ErrorRate  Error rate (%) on the number-finding distraction task

Statistics
    1. Shapiro-Wilk test per condition.
    2. If all conditions are normal -> repeated-measures ANOVA,
       otherwise -> Friedman test.
    3. Post-hoc pairwise tests (paired t-test after RM-ANOVA,
       Wilcoxon signed-rank after Friedman), Holm-Bonferroni-corrected.

Usage (from the Study/ folder):
    pip install -r analysis/requirements.txt
    python analysis/analysis.py

Inputs : data/cleaned/*.csv, data/cleaned/task_performance.xlsx
         data/raw/*.csv (only for the data-cleaning log)
Outputs: data/processed/*.csv, results/*.csv, results/figures/*.png
"""

from itertools import combinations
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from scipy import stats

# --------------------------------------------------------------------------
# Paths and constants
# --------------------------------------------------------------------------
STUDY_DIR = Path(__file__).resolve().parent.parent
RAW = STUDY_DIR / "data" / "raw"            # original LimeSurvey exports (unchanged)
CLEANED = STUDY_DIR / "data" / "cleaned"    # cleaned data used for the analysis
PROCESSED = STUDY_DIR / "data" / "processed"
RESULTS = STUDY_DIR / "results"
FIGURES = RESULTS / "figures"

CONDITIONS = ["A", "B", "C"]
CONDITION_LABELS = {"A": "A\n(Original)", "B": "B\n(Blurred)", "C": "C\n(Replacement)"}
SURVEY_FILES = {
    "A": "results-survey863865_condition_A_original.csv",
    "B": "results-survey459738_condition_B_blurred.csv",
    "C": "results-survey342267_condition_C_replacement.csv",
    "demographics": "results-survey732927_demographics.csv",
    "feedback": "results-survey738146_final_feedback.csv",
}
TASK_FILE = "task_performance.xlsx"

ALPHA = 0.05
N_PARTICIPANTS = 18

# Reversed items (LimeSurvey sub-question codes), see Study/README.md
TRUST_REVERSED = ["SQ005", "SQ007", "SQ012", "SQ015", "SQ016"]
ARI_REVERSED = ["SQ011", "SQ012", "SQ014", "SQ017", "SQ019"]

# Two raw responses were submitted without a participant ID (see Study/README.md).
# Needed only to match raw and cleaned rows in the data-cleaning log.
RAW_MISSING_IDS = {("A", 3): 2, ("C", 9): 9}


# --------------------------------------------------------------------------
# Loading
# --------------------------------------------------------------------------
def items(df, question_code):
    """Return the item columns of one LimeSurvey array question, sorted by sub-question number."""
    cols = [c for c in df.columns if c.startswith(question_code + "[")]
    return df[sorted(cols, key=lambda c: int(c.split("[SQ")[1][:3]))]


def sq_code(col):
    """'G03Q03[SQ005]. text' -> 'SQ005'"""
    return col.split("[")[1].split("]")[0]


def participant_column(df):
    return next(c for c in df.columns if "Participant ID" in c)


def load_survey(name, folder=CLEANED):
    """Load one survey export indexed by participant (only complete responses)."""
    df = pd.read_csv(folder / SURVEY_FILES[name])
    response_id, last_page, participant = df.columns[0], df.columns[2], participant_column(df)

    if folder == RAW:
        for (survey, resp), pid in RAW_MISSING_IDS.items():
            if survey == name:
                df.loc[df[response_id] == resp, participant] = pid

    # Drop incomplete duplicates (participant restarted the survey)
    df = df[df[last_page] == df[last_page].max()]
    df = df.rename(columns={participant: "participant"})
    df["participant"] = df["participant"].astype(int)
    assert df["participant"].is_unique, f"duplicate participants in {name}"
    assert len(df) == N_PARTICIPANTS, f"{name}: {len(df)} participants"
    return df.set_index("participant").sort_index()


def data_cleaning_log():
    """List every answer that differs between the raw export and the cleaned data."""
    rows = []
    for name in SURVEY_FILES:
        raw, clean = load_survey(name, RAW), load_survey(name, CLEANED)
        answer_cols = [c for c in raw.columns
                       if c[:1] == "G" and not c.split(".")[0].endswith("Time")]  # skip timing columns
        for pid in clean.index:
            for col in answer_cols:
                before, after = raw.loc[pid, col], clean.loc[pid, col]
                if pd.isna(before) and pd.isna(after):
                    continue
                if pd.isna(before):
                    change = "missing answer filled in"
                elif str(before).strip() != str(after).strip() and not (
                        _is_number(before) and _is_number(after) and float(before) == float(after)):
                    change = "answer changed"
                else:
                    continue
                rows.append({"survey": name, "participant": pid, "question": col.split(".")[0],
                             "item": col.split(".", 1)[1].strip(), "raw": before, "cleaned": after,
                             "change": change})
    return pd.DataFrame(rows)


def _is_number(value):
    try:
        float(value)
        return True
    except (TypeError, ValueError):
        return False


# --------------------------------------------------------------------------
# Scoring
# --------------------------------------------------------------------------
def score_sus(df):
    """System Usability Scale (Brooke, 1996): 0-100.

    Missing answers are replaced by the rounded item mean of that condition
    (one participant skipped two items in condition C).
    """
    x = items(df, "G00Q01").astype(float)
    x = x.fillna(x.mean().round())
    odd = x.iloc[:, 0::2] - 1   # positively worded items 1,3,5,7,9
    even = 5 - x.iloc[:, 1::2]  # negatively worded items 2,4,6,8,10
    return (odd.sum(axis=1) + even.sum(axis=1)) * 2.5


def score_tlx(df):
    """Raw NASA-TLX: mean of the 6 subscales (0-20). 'Performance' is reversed (20 - x)."""
    x = items(df, "G02Q02").astype(float)
    x.iloc[:, 3] = 20 - x.iloc[:, 3]  # SQ004 = performance (higher = better) -> reverse
    return x.mean(axis=1)


def score_likert(df, question_code, reversed_items, scale_max):
    """Mean of a Likert questionnaire (1..scale_max) with reversed items."""
    x = items(df, question_code).astype(float)
    for col in x.columns:
        if sq_code(col) in reversed_items:
            x[col] = scale_max + 1 - x[col]
    return x.mean(axis=1)  # missing answers are skipped


def load_error_rates():
    """Error rate (%) on the distraction task = incorrect / (correct + incorrect)."""
    df = pd.read_excel(CLEANED / TASK_FILE, header=None, skiprows=3, usecols=[0, 1, 2, 3])
    df.columns = ["participant", "condition", "correct", "incorrect"]
    df = df.dropna(subset=["participant"])
    df["participant"] = df["participant"].astype(int)
    df["ErrorRate"] = df["incorrect"] / (df["correct"] + df["incorrect"]) * 100
    return df.pivot(index="participant", columns="condition", values="ErrorRate")


def compute_scores():
    """Return a wide table: one row per participant, one column per measure x condition."""
    wide = pd.DataFrame(index=pd.RangeIndex(1, N_PARTICIPANTS + 1, name="participant"))
    for cond in CONDITIONS:
        df = load_survey(cond)
        wide[f"SUS_{cond}"] = score_sus(df)
        wide[f"TLX_{cond}"] = score_tlx(df)
        wide[f"Trust_{cond}"] = score_likert(df, "G03Q03", TRUST_REVERSED, 5)
        wide[f"ARI_{cond}"] = score_likert(df, "G05Q05", ARI_REVERSED, 7)
    errors = load_error_rates()
    for cond in CONDITIONS:
        wide[f"ErrorRate_{cond}"] = errors[cond]
    return wide


# --------------------------------------------------------------------------
# Statistics
# --------------------------------------------------------------------------
def rm_anova(data):
    """One-way repeated-measures ANOVA. data: participants x conditions array."""
    n, k = data.shape
    grand = data.mean()
    ss_cond = n * ((data.mean(axis=0) - grand) ** 2).sum()
    ss_subj = k * ((data.mean(axis=1) - grand) ** 2).sum()
    ss_total = ((data - grand) ** 2).sum()
    ss_error = ss_total - ss_cond - ss_subj
    df_cond, df_error = k - 1, (n - 1) * (k - 1)
    f = (ss_cond / df_cond) / (ss_error / df_error)
    return f, stats.f.sf(f, df_cond, df_error)


def holm(p_values):
    """Holm-Bonferroni correction: smallest p x m, next x (m-1), ... (kept monotone, capped at 1)."""
    m = len(p_values)
    order = np.argsort(p_values)
    corrected = np.empty(m)
    running_max = 0.0
    for rank, idx in enumerate(order):
        running_max = max(running_max, (m - rank) * p_values[idx])
        corrected[idx] = min(running_max, 1.0)
    return corrected


def stars(p):
    return "***" if p < 0.001 else "**" if p < 0.01 else "*" if p < 0.05 else "ns"


def analyse(measure, wide):
    """Run the omnibus test and post-hoc tests for one measure."""
    data = wide[[f"{measure}_{c}" for c in CONDITIONS]].to_numpy()
    normal = all(stats.shapiro(data[:, i]).pvalue > ALPHA for i in range(len(CONDITIONS)))

    if normal:
        test, (stat, p) = "RM-ANOVA", rm_anova(data)
        stat_label = f"F={stat:.2f}"
        pairwise = lambda a, b: stats.ttest_rel(a, b).pvalue
    else:
        test, (stat, p) = "Friedman", stats.friedmanchisquare(*data.T)
        stat_label = f"chi2={stat:.2f}"
        pairwise = lambda a, b: stats.wilcoxon(a, b).pvalue

    pairs = list(combinations(range(len(CONDITIONS)), 2))
    p_raw = [pairwise(data[:, i], data[:, j]) for i, j in pairs]
    p_corr = holm(p_raw)
    posthoc = []
    for (i, j), pr, pc in zip(pairs, p_raw, p_corr):
        posthoc.append({
            "Measure": measure,
            "Comparison": f"{CONDITIONS[i]} vs {CONDITIONS[j]}",
            "Test": "paired t-test" if normal else "Wilcoxon signed-rank",
            "p (raw)": round(pr, 5),
            "p (Holm)": round(pc, 5),
            "Significance": stars(pc),
        })

    summary = {"Measure": measure}
    for c, col in zip(CONDITIONS, data.T):
        summary[f"Mean {c} (SD)"] = f"{col.mean():.2f} ({col.std(ddof=1):.2f})"
    summary.update({
        "Test": test,
        "Statistic": stat_label,
        "p-value": f"{p:.4f}",
        "Significant": "Yes" if p < ALPHA else "No",
    })
    return summary, posthoc, data, test, p


# --------------------------------------------------------------------------
# Plots
# --------------------------------------------------------------------------
Y_LABELS = {
    "SUS": "SUS Score (0-100)",
    "TLX": "NASA-TLX RAW Score",
    "Trust": "Trust in Automation Score",
    "ARI": "ARI Immersion Score (1-7)",
    "ErrorRate": "Error Rate on Distraction Task (%)",
}
COLORS = ["#8DA0CB", "#E5B095", "#99C9A0"]


def boxplot(measure, data, test, p, posthoc):
    fig, ax = plt.subplots(figsize=(6, 5))
    bp = ax.boxplot(data, patch_artist=True, widths=0.5)
    for patch, color in zip(bp["boxes"], COLORS):
        patch.set_facecolor(color)
    ax.set_xticks(range(1, len(CONDITIONS) + 1), [CONDITION_LABELS[c] for c in CONDITIONS])
    ax.set_ylabel(Y_LABELS[measure])
    ax.set_title(f"{measure} by Condition\n({test}, p={p:.4f})")

    # Significance brackets in the order of the post-hoc table: A-B, A-C, B-C
    top, span = np.nanmax(data), np.nanmax(data) - np.nanmin(data)
    for level, ((i, j), result) in enumerate(zip(combinations(range(len(CONDITIONS)), 2), posthoc)):
        label = result["Significance"]
        y = top + span * (0.08 + 0.09 * level)
        ax.plot([i + 1, i + 1, j + 1, j + 1], [y - span * 0.02, y, y, y - span * 0.02], color="black")
        ax.text((i + j) / 2 + 1, y, label, ha="center", va="bottom")
    ax.set_ylim(top=top + span * 0.40)

    fig.tight_layout()
    fig.savefig(FIGURES / f"boxplot_{measure}.png", dpi=150)
    plt.close(fig)


# --------------------------------------------------------------------------
# Main
# --------------------------------------------------------------------------
def main():
    for folder in (PROCESSED, RESULTS, FIGURES):
        folder.mkdir(parents=True, exist_ok=True)

    wide = compute_scores()
    wide.to_csv(PROCESSED / "scores_wide_format.csv")
    long = wide.reset_index().melt(id_vars="participant", var_name="var", value_name="value")
    long[["measure", "condition"]] = long["var"].str.rsplit("_", n=1, expand=True)
    long = long.pivot_table(index=["participant", "condition"], columns="measure", values="value").reset_index()
    long.to_csv(PROCESSED / "scores_long_format.csv", index=False)

    summaries, posthocs = [], []
    for measure in ["SUS", "TLX", "Trust", "ARI", "ErrorRate"]:
        summary, posthoc, data, test, p = analyse(measure, wide)
        summaries.append(summary)
        posthocs.extend(posthoc)
        boxplot(measure, data, test, p, posthoc)

    log = data_cleaning_log()
    log.to_csv(RESULTS / "data_cleaning_log.csv", index=False)

    summary_df = pd.DataFrame(summaries)
    posthoc_df = pd.DataFrame(posthocs)
    summary_df.to_csv(RESULTS / "summary_table.csv", index=False)
    posthoc_df.to_csv(RESULTS / "posthoc_tests.csv", index=False)

    pd.set_option("display.width", 200)
    print(summary_df.to_string(index=False))
    print()
    print(posthoc_df.to_string(index=False))
    print()
    print("Data cleaning:", log["change"].value_counts().to_dict())


if __name__ == "__main__":
    main()
