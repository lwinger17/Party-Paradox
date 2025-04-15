document.querySelector('.start-button').addEventListener('click', function () {
    // Get the URL from the data attribute
    const redirectUrl = this.getAttribute('data-url');

    // Check if a countdown already exists
    if (document.querySelector('.countdown-timer')) {
        return; // Prevent multiple countdowns
    }

    // Create countdown timer
    let countdown = 10;
    const countdownElement = document.createElement('div');
    countdownElement.classList.add('countdown-timer');
    document.body.appendChild(countdownElement);

    // Create circle and number elements
    const circle = document.createElement('div');
    circle.classList.add('circle');
    countdownElement.appendChild(circle);

    const number = document.createElement('span');
    number.classList.add('number');
    number.innerText = countdown;
    countdownElement.appendChild(number);

    const interval = setInterval(() => {
        countdown--;
        number.innerText = countdown;

        // Adjust the circle size based on the countdown
        const circleSize = (countdown / 10) * 100;
        circle.style.width = `${circleSize}%`;
        circle.style.height = `${circleSize}%`;

        // Once countdown hits 0, redirect
        if (countdown === 0) {
            clearInterval(interval);
            countdownElement.innerText = 'Redirecting...';
            setTimeout(() => {
                window.location.href = redirectUrl; // Redirect to the correct URL
            }, 1000);
        }
    }, 1000);
});
