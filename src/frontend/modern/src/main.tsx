import React from 'react';
import { createRoot } from 'react-dom/client';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { App } from './App';
import { ChangshaTablePage } from './pages/ChangshaTablePage';
import './styles.css';

function Root() {
  const path = window.location.pathname;
  if (path === '/changsha' || path === '/changsha/') {
    return <ChangshaTablePage />;
  }
  return <App />;
}

createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <FluentProvider theme={webLightTheme}>
      <Root />
    </FluentProvider>
  </React.StrictMode>
);
