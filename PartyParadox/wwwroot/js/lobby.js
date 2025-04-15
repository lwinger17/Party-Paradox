const slider = document.getElementById("roomSize");
const valueDisplay = document.getElementById("slider-value");

slider.addEventListener("input", function() {
    valueDisplay.textContent = slider.value;
});