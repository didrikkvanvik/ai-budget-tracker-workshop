import Header from '../shared/components/layout/Header';

export async function loader() {
  return {};
}

export default function Dashboard() {
  return (
    <div className="px-4 py-6 sm:px-0">
      <Header
        title="Dashboard"
        subtitle="Welcome to your budget tracker"
      />

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <h3 className="text-lg font-semibold text-gray-900 mb-2">Getting Started</h3>
          <p className="text-gray-600">
            This is your dashboard template. Add your budget tracking features here.
          </p>
        </div>

        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <h3 className="text-lg font-semibold text-gray-900 mb-2">Features</h3>
          <p className="text-gray-600">
            Build transaction management, analytics, and reporting features.
          </p>
        </div>

        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <h3 className="text-lg font-semibold text-gray-900 mb-2">Ready to Code</h3>
          <p className="text-gray-600">
            Auth, styling, and project structure are already set up for you.
          </p>
        </div>
      </div>
    </div>
  );
}