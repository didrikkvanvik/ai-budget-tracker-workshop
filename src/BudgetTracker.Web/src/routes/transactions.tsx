import { type LoaderFunctionArgs } from 'react-router-dom';
import Header from '../shared/components/layout/Header';

export async function loader({ }: LoaderFunctionArgs) {
  return {};
}

export default function Transactions() {
  return (
    <div className="px-4 py-6 sm:px-0">
      <Header
        title="Transactions"
        subtitle="Manage your transactions"
      />

      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Transaction Management</h3>
        <p className="text-gray-600 mb-4">
          This is where you'll build transaction listing, filtering, and management features.
        </p>
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
          <p className="text-blue-800 text-sm">
            <strong>Workshop TODO:</strong> Implement transaction CRUD operations, search, filtering, and categorization.
          </p>
        </div>
      </div>
    </div>
  );
}