// Initialize variables first
let threadId = null;
let previousResponseId = null;
let attachedFiles = [];
let currentAgentMode = 'learn';
let chatMessages, messageInput, sendButton, fileInput, attachButton;
let attachedFilesContainer, mcpPanel, mcpToolsList;
let learnAgentBtn, travelAgentBtn, agentIndicator;

// Configure marked with syntax highlighting (if available)
if (typeof marked !== 'undefined') {
    marked.setOptions({
        highlight: function (code, lang) {
            if (typeof hljs !== 'undefined' && lang && hljs.getLanguage(lang)) {
                try {
                    return hljs.highlight(code, { language: lang }).value;
                } catch (err) { }
            }
            return typeof hljs !== 'undefined' ? hljs.highlightAuto(code).value : code;
        },
        breaks: true,
        gfm: true
    });
}

// Wait for DOM to be ready
document.addEventListener('DOMContentLoaded', function() {
    chatMessages = document.getElementById('chatMessages');
    messageInput = document.getElementById('messageInput');
    sendButton = document.getElementById('sendButton');
    fileInput = document.getElementById('fileInput');
    attachButton = document.getElementById('attachButton');
    attachedFilesContainer = document.getElementById('attachedFiles');
    mcpPanel = document.getElementById('mcpPanel');
    mcpToolsList = document.getElementById('mcpToolsList');
    learnAgentBtn = document.getElementById('learnAgentBtn');
    travelAgentBtn = document.getElementById('travelAgentBtn');
    agentIndicator = document.getElementById('agentIndicator');

    // Set up event listeners
    attachButton.addEventListener('click', () => {
        fileInput.click();
    });

    fileInput.addEventListener('change', (e) => {
        const files = Array.from(e.target.files);
        if (files.length > 0) {
            attachedFiles.push(...files);
            updateAttachedFilesDisplay();
            fileInput.value = '';
        }
    });

    sendButton.addEventListener('click', sendMessage);

    messageInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    // Focus input on load
    messageInput.focus();
});

// Agent Selection
function selectAgent(mode) {
    currentAgentMode = mode;

    // Get elements (in case called before DOMContentLoaded)
    const learnBtn = learnAgentBtn || document.getElementById('learnAgentBtn');
    const travelBtn = travelAgentBtn || document.getElementById('travelAgentBtn');
    const indicator = agentIndicator || document.getElementById('agentIndicator');

    // Update button states
    if (learnBtn && travelBtn) {
        learnBtn.classList.toggle('active', mode === 'learn');
        travelBtn.classList.toggle('active', mode === 'travel');
    }

    // Update indicator
    if (indicator) {
        indicator.textContent = mode === 'learn' ? 'Learn Agent' : 'Travel Agent';
    }

    // Reset conversation state when switching agents
    threadId = null;
    previousResponseId = null;

    // Show info message
    const modeInfo = mode === 'learn'
        ? '📚 Switched to Microsoft Learn Agent - Uses MCP (Model Context Protocol) to search Microsoft Learn documentation, tutorials, and Azure CLI commands'
        : '✈️ Switched to Travel Agent - Requires a Foundry-hosted Agent Application. Check the configuration if this fails.';
    addSystemMessage(modeInfo);

    console.log('Agent mode:', mode);
}

function addSystemMessage(text) {
    const messages = chatMessages || document.getElementById('chatMessages');
    if (!messages) return;

    const messageDiv = document.createElement('div');
    messageDiv.className = 'message agent';

    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';
    contentDiv.style.background = '#e8f4fd';
    contentDiv.style.fontStyle = 'italic';
    contentDiv.style.fontSize = '13px';
    contentDiv.textContent = text;

    messageDiv.appendChild(contentDiv);
    messages.appendChild(messageDiv);
    messages.scrollTop = messages.scrollHeight;
}

// MCP Panel Management
function toggleMcpPanel() {
    const panel = mcpPanel || document.getElementById('mcpPanel');
    if (!panel) return;

    panel.classList.toggle('show');
    if (panel.classList.contains('show')) {
        loadMcpTools();
    }
}

async function loadMcpTools() {
    const toolsList = mcpToolsList || document.getElementById('mcpToolsList');
    if (!toolsList) return;

    try {
        toolsList.innerHTML = '<p style="color: #999;">Loading...</p>';
        const response = await fetch('/api/mcp/discover');
        const data = await response.json();

        if (data.result && data.result.tools) {
            displayMcpTools(data.result.tools);
        } else {
            toolsList.innerHTML = '<p style="color: #c33;">No tools found</p>';
        }
    } catch (error) {
        console.error('Error loading MCP tools:', error);
        toolsList.innerHTML = '<p style="color: #c33;">Error loading tools</p>';
    }
}

function displayMcpTools(tools) {
    const toolsList = mcpToolsList || document.getElementById('mcpToolsList');
    if (!toolsList) return;

    toolsList.innerHTML = '';
    tools.forEach(tool => {
        const toolDiv = document.createElement('div');
        toolDiv.className = 'mcp-tool';
        toolDiv.onclick = () => useMcpTool(tool);

        const nameDiv = document.createElement('div');
        nameDiv.className = 'mcp-tool-name';
        nameDiv.textContent = tool.name;

        const descDiv = document.createElement('div');
        descDiv.className = 'mcp-tool-desc';
        descDiv.textContent = tool.description || 'No description';

        toolDiv.appendChild(nameDiv);
        toolDiv.appendChild(descDiv);
        toolsList.appendChild(toolDiv);
    });
}

function useMcpTool(tool) {
    const input = messageInput || document.getElementById('messageInput');
    if (!input) return;

    const prompt = `Use the ${tool.name} tool. ${tool.description || ''}`;
    input.value = prompt;
    toggleMcpPanel();
    input.focus();
}

function addMessage(content, isUser) {
    const messages = chatMessages || document.getElementById('chatMessages');
    if (!messages) return;

    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${isUser ? 'user' : 'agent'}`;

    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';

    if (isUser) {
        // User messages display as plain text
        contentDiv.textContent = content;
    } else {
        // Agent messages render as markdown (if available) or plain text
        if (typeof marked !== 'undefined') {
            contentDiv.innerHTML = marked.parse(content);
        } else {
            // Fallback to plain text with basic formatting
            contentDiv.textContent = content;
        }
    }

    messageDiv.appendChild(contentDiv);
    messages.appendChild(messageDiv);
    messages.scrollTop = messages.scrollHeight;
}

function addLoadingIndicator() {
    const messages = chatMessages || document.getElementById('chatMessages');
    if (!messages) return;

    const messageDiv = document.createElement('div');
    messageDiv.className = 'message agent';
    messageDiv.id = 'loadingIndicator';

    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';
    contentDiv.innerHTML = '<span class="loading"></span><span class="loading"></span><span class="loading"></span>';

    messageDiv.appendChild(contentDiv);
    messages.appendChild(messageDiv);
    messages.scrollTop = messages.scrollHeight;
}

function removeLoadingIndicator() {
    const loadingIndicator = document.getElementById('loadingIndicator');
    if (loadingIndicator) {
        loadingIndicator.remove();
    }
}

function showError(message) {
    const messages = chatMessages || document.getElementById('chatMessages');
    if (!messages) return;

    const errorDiv = document.createElement('div');
    errorDiv.className = 'error-message';
    errorDiv.textContent = message;
    messages.appendChild(errorDiv);
    messages.scrollTop = messages.scrollHeight;

    setTimeout(() => errorDiv.remove(), 5000);
}

function updateAttachedFilesDisplay() {
    if (attachedFiles.length === 0) {
        attachedFilesContainer.style.display = 'none';
        attachedFilesContainer.innerHTML = '';
        return;
    }

    attachedFilesContainer.style.display = 'flex';
    attachedFilesContainer.innerHTML = '';

    attachedFiles.forEach((file, index) => {
        const fileDiv = document.createElement('div');
        fileDiv.className = 'attached-file';

        const fileName = document.createElement('span');
        fileName.className = 'attached-file-name';
        fileName.textContent = file.name;
        fileName.title = file.name;

        const removeBtn = document.createElement('span');
        removeBtn.className = 'remove-file';
        removeBtn.textContent = '×';
        removeBtn.onclick = () => removeFile(index);

        fileDiv.appendChild(fileName);
        fileDiv.appendChild(removeBtn);
        attachedFilesContainer.appendChild(fileDiv);
    });
}

function removeFile(index) {
    attachedFiles.splice(index, 1);
    updateAttachedFilesDisplay();
}

async function sendMessage() {
    const message = messageInput.value.trim();
    if (!message && attachedFiles.length === 0) return;

    // Display user message
    let displayMessage = message;
    if (attachedFiles.length > 0) {
        const fileNames = attachedFiles.map(f => f.name).join(', ');
        displayMessage = message ? `${message}\n📎 ${fileNames}` : `📎 ${fileNames}`;
    }
    addMessage(displayMessage, true);

    messageInput.value = '';
    sendButton.disabled = true;
    messageInput.disabled = true;
    attachButton.disabled = true;
    addLoadingIndicator();

    try {
        let data;

        if (currentAgentMode === 'travel') {
            // Call the hosted travel agent API (Margies Travel Agent)
            const response = await fetch('/api/hosted-agent', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    message: message || 'Please analyze the attached file(s).',
                    previousResponseId: previousResponseId
                })
            });

            removeLoadingIndicator();

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.detail || `HTTP error! status: ${response.status}`);
            }

            data = await response.json();

            // Store response ID for conversation continuity
            if (data.threadId) {
                previousResponseId = data.threadId;
            }

            // Format response with citations if available
            let responseText = data.response;
            if (data.citations && data.citations.length > 0) {
                responseText += '\n\n**Sources:**\n';
                data.citations.forEach((c, i) => {
                    responseText += `${i + 1}. [${c.title || 'Source'}](${c.url})\n`;
                });
            }

            addMessage(responseText, false);
        } else {
            // Call the Microsoft Learn agent API via MCP (Model Context Protocol)
            const response = await fetch('/api/mcp/chat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    message: message || 'Please analyze the attached file(s).',
                    threadId: threadId,
                    autoApproveMcpTools: true
                })
            });

            removeLoadingIndicator();

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.detail || errorData.message || `HTTP error! status: ${response.status}`);
            }

            data = await response.json();
            addMessage(data.response, false);

            if (data.threadId) {
                threadId = data.threadId;
            }
        }

        // Clear attached files after successful send
        attachedFiles = [];
        updateAttachedFilesDisplay();
    } catch (error) {
        removeLoadingIndicator();
        console.error('Error:', error);
        showError(`Failed to send message: ${error.message}`);
    } finally {
        sendButton.disabled = false;
        messageInput.disabled = false;
        attachButton.disabled = false;
        messageInput.focus();
    }
}
