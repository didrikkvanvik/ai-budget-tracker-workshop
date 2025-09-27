import type { ProactiveRecommendation } from '../api';

interface RecommendationsCardProps {
  recommendations: ProactiveRecommendation[];
}

const AlertTriangleIcon = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
  </svg>
);

const DollarSignIcon = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
  </svg>
);

const LightbulbIcon = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
  </svg>
);

const TrendingDownIcon = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 17h8m0 0V9m0 8l-8-8-4 4-6-6" />
  </svg>
);

const getRecommendationIcon = (type: ProactiveRecommendation['type']) => {
  switch (type) {
    case 'SpendingAlert':
      return <AlertTriangleIcon />;
    case 'SavingsOpportunity':
      return <DollarSignIcon />;
    case 'BehavioralInsight':
      return <LightbulbIcon />;
    case 'BudgetWarning':
      return <TrendingDownIcon />;
  }
};

const getPriorityStyles = (priority: ProactiveRecommendation['priority']) => {
  switch (priority) {
    case 'Critical':
      return 'bg-red-50 border-red-200 text-red-800';
    case 'High':
      return 'bg-orange-50 border-orange-200 text-orange-800';
    case 'Medium':
      return 'bg-yellow-50 border-yellow-200 text-yellow-800';
    case 'Low':
      return 'bg-blue-50 border-blue-200 text-blue-800';
  }
};

export function RecommendationsCard({ recommendations }: RecommendationsCardProps) {
  if (!recommendations.length) {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h2 className="text-xl font-semibold text-gray-900 mb-4">Smart Recommendations</h2>
        <p className="text-gray-500 text-sm">
          No recommendations available yet. Import some transactions to get personalized financial advice.
        </p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <h2 className="text-xl font-semibold text-gray-900 mb-4">Smart Recommendations</h2>
      <div className="space-y-3">
        {recommendations.map((recommendation) => (
          <div
            key={recommendation.id}
            className={`p-4 rounded-lg border ${getPriorityStyles(recommendation.priority)}`}
          >
            <div className="flex items-start gap-3">
              <div className="flex-shrink-0 mt-0.5">
                {getRecommendationIcon(recommendation.type)}
              </div>
              <div className="flex-1 min-w-0">
                <h3 className="font-medium text-sm mb-1">{recommendation.title}</h3>
                <p className="text-sm opacity-90">{recommendation.message}</p>
                <div className="flex items-center gap-2 mt-2 text-xs opacity-75">
                  <span>{recommendation.type}</span>
                  <span>•</span>
                  <span>{recommendation.priority} Priority</span>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
