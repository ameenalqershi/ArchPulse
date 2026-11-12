// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './studio';
import './styles.css';

const root = document.getElementById('root');
if (!root) throw new Error('Root element is missing.');
createRoot(root).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
