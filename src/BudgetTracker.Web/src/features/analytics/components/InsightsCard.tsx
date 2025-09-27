import type { BudgetInsights } from '../types';

interface InsightsCardProps {
  insights: BudgetInsights;
}

const InfoIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10"></circle>
    <path d="m9 12 2 2 4-4"></path>
  </svg>
);

const AlertIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path>
    <path d="M12 9v4"></path>
    <path d="m12 17 .01 0"></path>
  </svg>
);

export function InsightsCard({ insights }: InsightsCardProps) {
  const { budgetBreakdown, summary, health } = insights;

  const formatAmount = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(amount);
  };

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-gray-900">Budget Insights</h3>
        <div className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
          health.isHealthy
            ? 'bg-green-100 text-green-700'
            : 'bg-yellow-100 text-yellow-700'
        }`}>
          {health.isHealthy ? <InfoIcon /> : <AlertIcon />}
          <span className="ml-1">{health.status}</span>
        </div>
      </div>

      {budgetBreakdown.totalExpenses > 0 && (
        <div className="space-y-4 mb-6">
          <div className="space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-sm font-medium text-gray-700">Needs</span>
              <span className="text-sm text-gray-900">
                {budgetBreakdown.needsPercentage}% • {formatAmount(budgetBreakdown.needsAmount)}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-blue-500 h-2 rounded-full"
                style={{ width: `${budgetBreakdown.needsPercentage}%` }}
              ></div>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-sm font-medium text-gray-700">Wants</span>
              <span className="text-sm text-gray-900">
                {budgetBreakdown.wantsPercentage}% • {formatAmount(budgetBreakdown.wantsAmount)}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-purple-500 h-2 rounded-full"
                style={{ width: `${budgetBreakdown.wantsPercentage}%` }}
              ></div>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-sm font-medium text-gray-700">Savings</span>
              <span className="text-sm text-gray-900">
                {budgetBreakdown.savingsPercentage}% • {formatAmount(budgetBreakdown.savingsAmount)}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-green-500 h-2 rounded-full"
                style={{ width: `${budgetBreakdown.savingsPercentage}%` }}
              ></div>
            </div>
          </div>
        </div>
      )}

      <div className="border-t border-gray-200 pt-4">
        <p className="text-sm text-gray-600 leading-relaxed">{summary}</p>

        {health.areas.length > 0 && (
          <div className="mt-3">
            <p className="text-xs font-medium text-gray-700 mb-2">Areas for improvement:</p>
            <ul className="space-y-1">
              {health.areas.map((area, index) => (
                <li key={index} className="text-xs text-gray-600 flex items-start">
                  <span className="text-yellow-500 mr-1">•</span>
                  {area}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}