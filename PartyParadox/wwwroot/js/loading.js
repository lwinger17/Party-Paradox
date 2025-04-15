function showLoadingScreen() {
    const loadingScreen = document.getElementById('loading-screen');
    if (loadingScreen) {
      loadingScreen.style.display = 'flex'; // Show the loading screen
    }
  }
  
  function hideLoadingScreen() {
    const loadingScreen = document.getElementById('loading-screen');
    if (loadingScreen) {
      loadingScreen.style.display = 'none'; // Hide the loading screen
    }
  }
  
  // Example usage on page load
  document.addEventListener('DOMContentLoaded', () => {
    showLoadingScreen();
  
    // Simulate page loading time
    setTimeout(hideLoadingScreen, 2000); // Adjust the timeout to match your needs
  });
  