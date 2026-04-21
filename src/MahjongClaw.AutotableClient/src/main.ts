import { initScene } from './scene';
import { initSignalR } from './net';

async function main() {
  console.log('Mahjong Claw Autotable Client - Phase 0');

  // Initialize three.js scene
  const container = document.getElementById('canvas-container');
  if (!container) {
    console.error('Canvas container not found');
    return;
  }

  initScene(container);

  // Initialize SignalR connection
  await initSignalR();
}

main().catch(console.error);
