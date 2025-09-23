import Header from '../shared/components/layout/Header';

export default function Import() {
  return (
    <div className="px-4 py-6 sm:px-0">
      <Header
        title="Import Transactions"
        subtitle="Upload and import transaction data"
      />

      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">CSV Import</h3>
        <p className="text-gray-600 mb-4">
          This is where you'll build CSV file upload and import functionality.
        </p>
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-6">
          <p className="text-blue-800 text-sm">
            <strong>Workshop TODO:</strong> Implement file upload, CSV parsing, bank format detection, and bulk transaction import.
          </p>
        </div>

        {/* Placeholder for file upload component */}
        <div className="border-2 border-dashed border-gray-300 rounded-lg p-6 text-center">
          <svg className="mx-auto h-12 w-12 text-gray-400" stroke="currentColor" fill="none" viewBox="0 0 48 48">
            <path d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          <div className="mt-4">
            <p className="text-sm text-gray-600">File upload functionality will be implemented here</p>
          </div>
        </div>
      </div>
    </div>
  );
}