import React from 'react';
import { DashboardView } from './Views/DashboardView';

export const App: React.FC = () => {
  return (
    <div className="min-h-screen bg-[#f2f4f8] font-sans text-slate-700 flex flex-col antialiased">
      <DashboardView />
    </div>
  );
};


