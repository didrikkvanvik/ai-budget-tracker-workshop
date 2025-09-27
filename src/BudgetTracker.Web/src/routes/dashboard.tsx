import { useLoaderData, useNavigation, type LoaderFunctionArgs } from 'react-router';
import { InsightsCard, analyticsApi } from '../features/analytics';
import Header from '../shared/components/layout/Header';
import { QueryAssistant } from '../features/intelligence';
import type { BudgetInsights } from '../features/analytics';


export async function loader({ }: LoaderFunctionArgs) {
  try {
    const insights = await analyticsApi.getInsights().catch(() => null); // Don't fail dashboard if insights fail
    return { insights };
  } catch (error) {
    console.error('Failed to load dashboard data:', error);
    throw new Error('Failed to load dashboard data');
  }
}

export default function Dashboard() {
  const data = useLoaderData() as {
    insights: BudgetInsights | null;
  };
  const { insights } = data;
  const navigation = useNavigation();
  const isLoading = navigation.state === 'loading';

  if (isLoading) {
    return (
      <div className="px-4 py-6 sm:px-0">
        <div className="mb-10">
          <div className="animate-pulse bg-neutral-200 rounded-xl h-8 w-48 mb-3" />
          <div className="animate-pulse bg-neutral-200 rounded-lg h-4 w-32" />
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="animate-pulse bg-neutral-200 rounded-xl h-64" />
          <div className="animate-pulse bg-neutral-200 rounded-xl h-64" />
        </div>
      </div>
    );
  }

  return (
    <div className="px-4 py-6 sm:px-0">
      <Header
        title="Dashboard"
        subtitle="Analytics insights demo"
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {insights && <InsightsCard insights={insights} />}
        <QueryAssistant />
      </div>
    </div>
  );
}