const audioElements = {
    like: document.getElementById('like-audio'),
    wave: document.getElementById('wave-audio'),
    home: document.getElementById('home-audio')
};

// Determine which audio element exists on the page
let activeAudio = Object.values(audioElements).find(audio => audio !== null);
const audioToggle = document.getElementById('audio-toggle');
const audioIcon = document.getElementById('audio-icon');
const audioState = localStorage.getItem('audioState') || 'on';
const savedTime = localStorage.getItem('audioTime') || 0; // Get saved playback time

// Function to toggle audio
function toggleAudio() {
    if (activeAudio.paused) {
        activeAudio.play();
        audioIcon.src = '/img/audio.png'; // Audio On
        localStorage.setItem('audioState', 'on');
    } else {
        activeAudio.pause();
        audioIcon.src = '/img/audioOff.png'; // Audio Off
        localStorage.setItem('audioState', 'off');
    }
}

// Initialize audio
document.addEventListener("DOMContentLoaded", () => {
    if (activeAudio) {
        activeAudio.loop = true;

        // Restore playback position
        activeAudio.currentTime = parseFloat(savedTime);

        if (audioState === 'on') {
            activeAudio.play().catch(error => {
                console.log('Autoplay blocked.', error);
                audioIcon.src = '/img/audioOff.png';
            });
        } else {
            activeAudio.pause();
            audioIcon.src = '/img/audioOff.png';
        }

        // Add event listener only if the toggle button exists
        if (audioToggle) {
            audioToggle.addEventListener('click', toggleAudio);
        }
    }
});

// Save playback time before leaving the page
window.addEventListener("beforeunload", () => {
    if (activeAudio) {
        localStorage.setItem('audioTime', activeAudio.currentTime);
    }
});

// Ensure audio plays after user interaction
document.body.addEventListener("click", () => {
    if (activeAudio && localStorage.getItem('audioState') === 'on' && activeAudio.paused) {
        activeAudio.play();
    }
}, { once: true });
