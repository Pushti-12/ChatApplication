"use strict";

(function () {

    // =====================================================
    // PREVENT DUPLICATE INITIALIZATION
    // =====================================================

    if (window.chatSignalRInitialized) {

        console.log(
            "Chat SignalR already initialized."
        );

        return;
    }

    window.chatSignalRInitialized = true;


    // =====================================================
    // ELEMENTS
    // =====================================================

    const messagesArea =
        document.getElementById(
            "messagesArea"
        );

    const messageInput =
        document.getElementById(
            "messageText"
        );
        // =====================================================
// AUTO RESIZE MESSAGE BOX
// =====================================================

messageInput?.addEventListener(
    "input",
    function () {

        this.style.height = "auto";

        this.style.height =
            Math.min(
                this.scrollHeight,
                140
            ) + "px";

    }
);

    const typingStatus =
        document.getElementById(
            "typingStatus"
        );

    const typingIndicator =
        document.getElementById(
            "typingIndicator"
        );

    const connectionStatus =
        document.getElementById(
            "connectionStatus"
        );

    const headerOnlineIndicator =
        document.getElementById(
            "headerOnlineIndicator"
        );


    const currentUserId =
        Number(window.currentUserId);

    const otherUserId =
        Number(window.otherUserId);

    const conversationId =
        Number(window.chatConversationId);


    // =====================================================
    // SIGNALR
    // =====================================================

    const connection =
        new signalR.HubConnectionBuilder()

            .withUrl("/chatHub")

            .withAutomaticReconnect()

            .build();


    // =====================================================
    // ONLINE USERS
    // =====================================================

    function setUserOnline(userId, online) {

        userId = Number(userId);

        const userItem =
            document.querySelector(
                `.user-item[data-user-id="${userId}"]`
            );


        if (userItem) {

            const indicator =
                userItem.querySelector(
                    ".online-indicator"
                );

            if (indicator) {

                indicator.classList.toggle(
                    "online",
                    online
                );

                indicator.classList.toggle(
                    "offline",
                    !online
                );

            }

        }


        // Current conversation user

        if (userId === otherUserId) {

            if (headerOnlineIndicator) {

                headerOnlineIndicator.classList.toggle(
                    "online",
                    online
                );

                headerOnlineIndicator.classList.toggle(
                    "offline",
                    !online
                );

            }


            if (typingStatus &&
                !typingStatus.dataset.typing) {

                typingStatus.textContent =
                    online
                        ? "Online"
                        : "Offline";

            }

        }

    }


    // =====================================================
    // USER ONLINE
    // =====================================================

    connection.on(
        "UserOnline",
        function (userId) {

            console.log(
                "User online:",
                userId
            );

            setUserOnline(
                userId,
                true
            );

        }
    );


    // =====================================================
    // USER OFFLINE
    // =====================================================

    connection.on(
        "UserOffline",
        function (userId) {

            console.log(
                "User offline:",
                userId
            );

            setUserOnline(
                userId,
                false
            );

        }
    );


    // =====================================================
    // INITIAL ONLINE USERS
    // =====================================================

    async function loadOnlineUsers() {

        try {

            const onlineUsers =
                await connection.invoke(
                    "GetOnlineUsers"
                );


            onlineUsers.forEach(
                userId => {

                    setUserOnline(
                        userId,
                        true
                    );

                }
            );

        }
        catch (error) {

            console.error(
                "Could not load online users:",
                error
            );

        }

    }


    // =====================================================
    // RECEIVE MESSAGE
    // =====================================================

    connection.on(
        "ReceiveMessage",
        async function (message) {

            console.log(
                "Realtime message:",
                message
            );


            if (!message) {
                return;
            }


            // Only current conversation

            if (
                Number(message.conversationId) !==
                conversationId
            ) {

                return;

            }


            if (!messagesArea) {
                return;
            }


            // Remove empty state

            const noMessages =
                messagesArea.querySelector(
                    ".no-messages"
                );

            if (noMessages) {
                noMessages.remove();
            }


            // =================================================
            // MESSAGE ROW
            // =================================================

            const row =
                document.createElement(
                    "div"
                );

            row.className =
                "message-row theirs";


            row.dataset.messageId =
                message.messageId;


            row.dataset.senderId =
                message.senderId;


            // =================================================
            // BUBBLE
            // =================================================

            const bubble =
                document.createElement(
                    "div"
                );

            bubble.className =
                "message-bubble";


            // =================================================
            // TEXT
            // =================================================

            const text =
                document.createElement(
                    "div"
                );

            text.className =
                "message-text";

            text.textContent =
                message.messageText || "";


            // =================================================
            // TIME
            // =================================================

            const time =
                document.createElement(
                    "div"
                );

            time.className =
                "message-time";

            time.textContent =
                message.sentAt || "";


            bubble.appendChild(text);

            bubble.appendChild(time);

            row.appendChild(bubble);

            messagesArea.appendChild(row);


            // Scroll

            messagesArea.scrollTo({
                top:
                    messagesArea.scrollHeight,

                behavior:
                    "smooth"
            });


            // =================================================
            // DELIVERED
            // =================================================

            try {

                await connection.invoke(
                    "MarkDelivered",
                    Number(message.messageId),
                    Number(message.senderId)
                );

            }
            catch (error) {

                console.error(
                    "MarkDelivered failed:",
                    error
                );

            }


            // =================================================
            // READ
            // =================================================

            try {

                await connection.invoke(
                    "MarkRead",
                    Number(message.messageId),
                    Number(message.senderId)
                );

            }
            catch (error) {

                console.error(
                    "MarkRead failed:",
                    error
                );

            }

        }
    );


    // =====================================================
    // MESSAGE DELIVERED
    // =====================================================

    connection.on(
        "MessageDelivered",
        function (messageId) {

            console.log(
                "Message delivered:",
                messageId
            );


            updateMessageStatus(
                messageId,
                "Delivered"
            );

        }
    );


    // =====================================================
    // MESSAGE READ
    // =====================================================

    connection.on(
        "MessageRead",
        function (messageId) {

            console.log(
                "Message read:",
                messageId
            );


            updateMessageStatus(
                messageId,
                "Read"
            );

        }
    );


    // =====================================================
    // UPDATE MESSAGE STATUS
    // =====================================================

    function updateMessageStatus(
        messageId,
        status
    ) {

        const messageRow =
            document.querySelector(
                `[data-message-id="${messageId}"]`
            );


        if (!messageRow) {
            return;
        }


        const time =
            messageRow.querySelector(
                ".message-time"
            );


        if (!time) {
            return;
        }


        let statusElement =
            messageRow.querySelector(
                ".message-status"
            );


        if (!statusElement) {

            statusElement =
                document.createElement(
                    "span"
                );

            statusElement.className =
                "message-status";

            time.appendChild(
                statusElement
            );

        }


        if (status === "Read") {

            statusElement.innerHTML =
                '<span class="read-status">✓✓</span>';

            statusElement.dataset.status =
                "Read";

        }

        else if (
            status === "Delivered"
        ) {

            statusElement.innerHTML =
                '<span class="delivered-status">✓✓</span>';

            statusElement.dataset.status =
                "Delivered";

        }

    }


    // =====================================================
    // TYPING
    // =====================================================

    let typingTimeout = null;


    messageInput?.addEventListener(
        "input",
        async function () {

            if (
                connection.state !==
                signalR.HubConnectionState.Connected
            ) {

                return;

            }


            try {

                await connection.invoke(
                    "StartTyping",
                    otherUserId
                );

            }
            catch (error) {

                console.error(
                    "StartTyping failed:",
                    error
                );

            }


            clearTimeout(
                typingTimeout
            );


            typingTimeout =
                setTimeout(
                    async function () {

                        try {

                            await connection.invoke(
                                "StopTyping",
                                otherUserId
                            );

                        }
                        catch (error) {

                            console.error(
                                "StopTyping failed:",
                                error
                            );

                        }

                    },
                    1200
                );

        }
    );


    // =====================================================
    // USER TYPING
    // =====================================================

    connection.on(
        "UserTyping",
        function (userId) {

            if (
                Number(userId) !==
                otherUserId
            ) {

                return;

            }


            if (typingStatus) {

                typingStatus.dataset.typing =
                    "true";

                typingStatus.textContent =
                    "Typing...";

            }


            if (typingIndicator) {

                typingIndicator.classList.add(
                    "show"
                );

            }

        }
    );


    // =====================================================
    // USER STOPPED TYPING
    // =====================================================

    connection.on(
        "UserStoppedTyping",
        function (userId) {

            if (
                Number(userId) !==
                otherUserId
            ) {

                return;

            }


            if (typingStatus) {

                delete typingStatus.dataset.typing;

                typingStatus.textContent =
                    "Online";

            }


            if (typingIndicator) {

                typingIndicator.classList.remove(
                    "show"
                );

            }

        }
    );


    // =====================================================
    // CONNECTION STATUS
    // =====================================================

    function updateConnectionStatus(
        status
    ) {

        if (!connectionStatus) {
            return;
        }


        if (status === "connected") {

            connectionStatus.innerHTML =
                '<span class="status-dot status-connected"></span> Online';

        }

        else if (
            status === "reconnecting"
        ) {

            connectionStatus.innerHTML =
                '<span class="status-dot status-reconnecting"></span> Connecting...';

        }

        else {

            connectionStatus.innerHTML =
                '<span class="status-dot status-disconnected"></span> Offline';

        }

    }


    // =====================================================
    // RECONNECTING
    // =====================================================

    connection.onreconnecting(
        function (error) {

            console.warn(
                "SignalR reconnecting:",
                error
            );

            updateConnectionStatus(
                "reconnecting"
            );

        }
    );


    // =====================================================
    // RECONNECTED
    // =====================================================

    connection.onreconnected(
        async function (connectionId) {

            console.log(
                "SignalR reconnected:",
                connectionId
            );


            updateConnectionStatus(
                "connected"
            );


            await loadOnlineUsers();

        }
    );


    // =====================================================
    // CLOSED
    // =====================================================

    connection.onclose(
        function (error) {

            console.error(
                "SignalR closed:",
                error
            );


            updateConnectionStatus(
                "disconnected"
            );

        }
    );


    // =====================================================
    // START CONNECTION
    // =====================================================

    async function startConnection() {

        try {

            console.log(
                "Starting SignalR..."
            );


            await connection.start();


            console.log(
                "SignalR connected successfully."
            );


            updateConnectionStatus(
                "connected"
            );


            await loadOnlineUsers();

        }
        catch (error) {

            console.error(
                "SignalR connection failed:",
                error
            );


            updateConnectionStatus(
                "disconnected"
            );


            setTimeout(
                startConnection,
                5000
            );

        }

    }


    // =====================================================
    // START
    // =====================================================

    startConnection();

})();