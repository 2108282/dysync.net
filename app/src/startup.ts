const root = document.documentElement;
const shouldDelayBeforeLogin = root.dataset.loginStartupDelay === 'true';
const startupDelayMs = shouldDelayBeforeLogin
  ? 1000 + Math.floor(Math.random() * 1001)
  : 0;

function clearStartupScreen() {
  window.requestAnimationFrame(() => {
    window.requestAnimationFrame(() => {
      root.classList.remove(
        'login-startup-active',
        'app-preload-light',
        'app-preload-dark',
      );
    });
  });
}

function showStartupError(error: unknown) {
  console.error('Application startup failed.', error);

  const statusElement = document.getElementById('startup-status-text');
  if (statusElement) {
    statusElement.textContent = '启动失败，请刷新页面重试';
  }
}

async function startApplication() {
  if (shouldDelayBeforeLogin) {
    try {
      window.sessionStorage.setItem('dysync-login-startup-shown-v2', '1');
    } catch (error) {
      console.warn('Unable to record startup state.', error);
    }

    await new Promise((resolve) => window.setTimeout(resolve, startupDelayMs));
  }

  try {
    await import('./main');
    clearStartupScreen();
  } catch (error) {
    showStartupError(error);
  }
}

void startApplication();
