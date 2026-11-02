import sys

import pandas as pd
import numpy as np
import statsmodels.api as sm
import seaborn as sns
import matplotlib.pyplot as plt

def simple_regression(file):
    # Load your CSV (adjust separator if needed – yours uses ';')
    print('Reading data...')
    df = pd.read_csv(file, sep=';')

    # Show basic info
    print('Show basic data...')
    print(df.info())
    print(df.describe())

    corr = df.corr(numeric_only=True)  # win is binary, pandas handles it as point-biserial
    print(corr[['win']].sort_values('win', ascending=False))

    # Visualise all correlations
    plt.figure(figsize=(10, 8))
    sns.heatmap(corr, annot=True, cmap='coolwarm', center=0)
    plt.title('Correlation matrix')
    plt.show()

    # Define predictors (choose a subset to avoid overfitting)
    # For 50 rows, use 2‑3 predictors maximum.
    X = df[['successful_attacks', 'pct_enemy_targeted', 'avg_request_time']]
    # Or try: X = df[['kills', 'attack_attempts', 'avg_request_time']]
    y = df['win']

    # Add constant for intercept
    X = sm.add_constant(X)

    # Fit logistic regression
    model = sm.Logit(y, X).fit()
    print(model.summary())

    from statsmodels.stats.outliers_influence import variance_inflation_factor

    vif_data = pd.DataFrame()
    vif_data['feature'] = X.columns[1:]  # exclude const
    vif_data['VIF'] = [variance_inflation_factor(X.values, i + 1) for i in range(len(vif_data))]
    print(vif_data)

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Error: Please provide the file: regression.csv")
        print(f"Usage: {sys.argv[0]} <regression.csv>")
        sys.exit(1)

    file = sys.argv[1]
    print(f"Loading data from ... {file}")
    simple_regression(file)