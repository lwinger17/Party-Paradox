document.querySelector('.start-button').addEventListener('click', function () {
    // Get the URLs from data attributes
    const specialRedirectUrl = this.getAttribute('data-url-special'); // /wavelength/Bprompts
    const regularRedirectUrl = this.getAttribute('data-url-regular'); // /wavelength/ChooseNum

    // Check if a countdown already exists
    if (document.querySelector('.countdown-timer')) {
        return; // Prevent multiple countdowns
    }

    // Create countdown timer container
    let countdown = 10;
    const countdownElement = document.createElement('div');
    countdownElement.classList.add('countdown-timer');
    document.body.appendChild(countdownElement);

    // Circle for countdown animation
    const circle = document.createElement('div');
    circle.classList.add('circle');
    countdownElement.appendChild(circle);

    // Number display
    const number = document.createElement('span');
    number.classList.add('number');
    number.innerText = countdown;
    countdownElement.appendChild(number);

    // List of participants (Replace this with actual logic)
    const participants = ['User1', 'User2', 'User3', 'User4']; // Example user list, replace with actual data
    const specialUser = participants[Math.floor(Math.random() * participants.length)]; // Pick one special user

    // Get current user dynamically (Replace this with actual logic to get logged-in user)
    const currentUser = document.getElementById('current-user')?.textContent || 'User2';

    // Countdown interval
    const interval = setInterval(() => {
        countdown--;
        number.innerText = countdown;

        // Smooth shrinking animation
        const scaleValue = countdown / 10;
        circle.style.transform = `scale(${scaleValue})`;

        if (countdown === 0) {
            clearInterval(interval);
            countdownElement.innerText = 'Redirecting...';

            setTimeout(() => {
                // Determine which URL to redirect to
                const redirectUrl = currentUser === specialUser ? specialRedirectUrl : regularRedirectUrl;
                window.location.href = redirectUrl;
            }, 1000);

            // Remove countdown element after redirection
            setTimeout(() => {
                countdownElement.remove();
            }, 2000);
        }
    }, 1000);
});
