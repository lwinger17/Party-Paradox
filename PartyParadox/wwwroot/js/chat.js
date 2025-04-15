// Function to get the user color based on UserID (from the backend)
function getUserColor(userID) {
    // Example: this can be dynamic based on the session and user, e.g., pulled from the database
    const userColors = [
        '#FF5733', '#33FF57', '#3357FF', '#F1C40F', '#8E44AD',
        '#1ABC9C', '#E74C3C', '#3498DB', '#2ECC71', '#9B59B6',
        '#E67E22', '#95A5A6'
    ];
    // Get the color for the user based on UserID (assuming userID is between 1 and 12)
    return userColors[(userID - 1) % userColors.length];
}

// Send a message function
sendBtn.addEventListener('click', () => {
    const message = chatInput.value.trim();

    if (message) {
        // Assume the current user has a UserID, you can dynamically set this from the backend (e.g., session data)
        const userID = 1; // Replace this with the dynamic userID from backend or session

        const userColor = getUserColor(userID); // Get color for the current user

        // Create new message from user (sent message)
        const messageElement = document.createElement('div');
        messageElement.classList.add('chat-message', 'Chatself-message');

        const nameBubble = document.createElement('div');
        nameBubble.classList.add('chat-bubble', 'Chatself-bubble');
        nameBubble.textContent = 'You';  // Display user name

        const messageBubble = document.createElement('div');
        messageBubble.classList.add('chat-bubble');
        messageBubble.textContent = message;
        messageBubble.style.backgroundColor = userColor; // Apply user color to message bubble

        messageElement.appendChild(nameBubble);
        messageElement.appendChild(messageBubble);

        chatBox.appendChild(messageElement);

        chatInput.value = ''; // Clear input field
        chatBox.scrollTop = chatBox.scrollHeight; // Scroll to bottom
    }
});

// Function to simulate a message from another user
function addMessageFromOtherUser(userID, message) {
    const userColor = getUserColor(userID); // Get color for the other user

    const messageElement = document.createElement('div');
    messageElement.classList.add('chat-message', 'Chatuser-message');

    const nameBubble = document.createElement('div');
    nameBubble.classList.add('chat-bubble', 'Chatuser-bubble');
    nameBubble.textContent = 'Other User'; // Display other user name

    const messageBubble = document.createElement('div');
    messageBubble.classList.add('chat-bubble');
    messageBubble.textContent = message;
    messageBubble.style.backgroundColor = userColor; // Apply user color to message bubble

    messageElement.appendChild(nameBubble);
    messageElement.appendChild(messageBubble);

    chatBox.appendChild(messageElement);
    chatBox.scrollTop = chatBox.scrollHeight; // Scroll to bottom
}

// Example: Add a message from another user after 3 seconds
setTimeout(() => {
    addMessageFromOtherUser(2, 'Hello! How are you?'); // Pass UserID for other user
}, 3000);
