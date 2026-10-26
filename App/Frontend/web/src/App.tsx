import React from 'react';
import { MainLayout } from './layouts/MainLayout';
import { DashboardView } from './features/dashboard';

export const App: React.FC = () => {
  return (
    <MainLayout>
      <DashboardView />
    </MainLayout>
  );
};


