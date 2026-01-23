const { chromium } = require('playwright');

(async () => {
    console.log('Starting Playwright test...');

    const baseUrl = process.env.BASE_URL || 'http://localhost:5109';
    console.log(`Base URL: ${baseUrl}`);

    const browser = await chromium.launch({ headless: false });
    const context = await browser.newContext();
    const page = await context.newPage();

    page.on('console', (msg) => {
        console.log(`[browser console][${msg.type()}] ${msg.text()}`);
    });
    page.on('pageerror', (err) => {
        console.log(`[browser pageerror] ${err.message}`);
    });
    page.on('requestfailed', (req) => {
        const failure = req.failure();
        console.log(`[request failed] ${req.method()} ${req.url()} :: ${failure ? failure.errorText : 'unknown'}`);
    });
    page.on('response', async (res) => {
        const url = res.url();
        if (url.includes('/api/mcp/discover') || url.includes('/api/chat')) {
            console.log(`[response] ${res.status()} ${res.request().method()} ${url}`);
        }
    });

    try {
        // Navigate to the app
        console.log(`Navigating to ${baseUrl}`);
        await page.goto(baseUrl);
        await page.waitForLoadState('networkidle');

        // Wait for the chat interface to load
        await page.waitForSelector('#messageInput', { timeout: 5000 });
        console.log('Chat interface loaded');

        // MCP panel smoke test
        console.log('\n=== MCP: Opening MCP panel and loading tools ===');
        const [discoverResponse] = await Promise.all([
            page.waitForResponse((r) => r.url().includes('/api/mcp/discover'), { timeout: 10000 }),
            page.click('#mcpButton')
        ]);
        console.log(`MCP discover status: ${discoverResponse.status()}`);
        if (!discoverResponse.ok()) {
            const body = await discoverResponse.text();
            throw new Error(`MCP discover failed: ${discoverResponse.status()} ${body}`);
        }
        // Wait for tools to render
        await page.waitForSelector('#mcpPanel.show', { timeout: 5000 });
        await page.waitForSelector('.mcp-tool', { timeout: 5000 });
        const toolNames = await page.locator('.mcp-tool-name').allTextContents();
        console.log(`MCP tools loaded (${toolNames.length}): ${toolNames.join(', ')}`);
        await page.screenshot({ path: 'd:\\Repos\\foundryAgent\\mcp-panel.png' });
        console.log('Saved screenshot: mcp-panel.png');

        // Use an MCP tool card to seed the prompt, then send
        console.log('\n=== TEST 0: Clicking an MCP tool card and sending ===');
        await page.locator('.mcp-tool').first().click();
        await page.waitForSelector('#mcpPanel.show', { state: 'hidden', timeout: 5000 });
        let seededPrompt = await page.inputValue('#messageInput');
        console.log(`Seeded prompt: ${seededPrompt}`);
        if (!seededPrompt || !seededPrompt.toLowerCase().includes('use the')) {
            throw new Error('Expected MCP tool click to seed a prompt into #messageInput');
        }

        // Force a concrete repo enumeration query
        // Using GitHub search syntax as many MCP implementations expose search_repositories.
        seededPrompt = `Use the search_repositories tool. Query: user:sayedimac sort:updated-desc. Return top 10 repos with name and url.`;
        await page.fill('#messageInput', seededPrompt);

        await page.click('#sendButton');
        await page.waitForSelector('#loadingIndicator', { timeout: 5000 });
        await page.waitForSelector('#loadingIndicator', { state: 'hidden', timeout: 60000 });

        // Basic sanity check that we got something back
        const agentMsgs0 = await page.locator('.message.agent .message-content').all();
        if (agentMsgs0.length < 2) {
            throw new Error('Expected an agent response after MCP repo query');
        }
        const last0 = await agentMsgs0[agentMsgs0.length - 1].textContent();
        console.log('\n--- Agent Response (Repo Query) ---');
        console.log(last0);
        console.log('----------------------------------\n');

        await page.screenshot({ path: 'd:\\Repos\\foundryAgent\\mcp-tool-response.png' });
        console.log('Saved screenshot: mcp-tool-response.png');

        // Test 1: Simple text message
        console.log('\n=== TEST 1: Sending a text message ===');
        await page.fill('#messageInput', 'Hello! Tell me a joke about programming.');
        console.log('Message typed');

        await page.click('#sendButton');
        console.log('Send button clicked');

        // Wait for loading indicator to appear and disappear
        await page.waitForSelector('#loadingIndicator', { timeout: 5000 });
        console.log('Loading indicator appeared');

        await page.waitForSelector('#loadingIndicator', { state: 'hidden', timeout: 60000 });
        console.log('Response received!');

        // Get the response
        const messages = await page.locator('.message.agent .message-content').all();
        if (messages.length > 1) {
            const responseText = await messages[messages.length - 1].textContent();
            console.log('\n--- Agent Response ---');
            console.log(responseText);
            console.log('----------------------\n');
        }

        // Wait a moment
        await page.waitForTimeout(2000);

        // Test 1b: Markdown-heavy response
        console.log('\n=== TEST 1b: Asking for markdown output ===');
        await page.fill('#messageInput', 'Return a short markdown response with a bullet list and a 2-row table about fruits.');
        await page.click('#sendButton');
        await page.waitForSelector('#loadingIndicator', { timeout: 5000 });
        await page.waitForSelector('#loadingIndicator', { state: 'hidden', timeout: 60000 });
        await page.screenshot({ path: 'd:\\Repos\\foundryAgent\\markdown-response.png' });
        console.log('Saved screenshot: markdown-response.png');

        // Test 2: File upload
        console.log('\n=== TEST 2: Testing file upload ===');

        // Click attach button
        await page.click('#attachButton');
        console.log('Attach button clicked');

        // Upload the demo file
        const fileInput = await page.locator('#fileInput');
        await fileInput.setInputFiles('d:\\Repos\\foundryAgent\\demo-sales-data.csv');
        console.log('File selected: demo-sales-data.csv');

        // Wait for file to appear in attached files
        await page.waitForSelector('.attached-file', { timeout: 2000 });
        console.log('File attached successfully');

        // Type a message
        await page.fill('#messageInput', 'Analyze this sales data and tell me which product had the highest total revenue.');
        console.log('Message typed');

        // Send
        await page.click('#sendButton');
        console.log('Send button clicked');

        // Wait for response
        await page.waitForSelector('#loadingIndicator', { timeout: 5000 });
        console.log('Processing file...');

        await page.waitForSelector('#loadingIndicator', { state: 'hidden', timeout: 120000 });
        console.log('File analysis complete!');

        // Get the response
        const allMessages = await page.locator('.message.agent .message-content').all();
        if (allMessages.length > 0) {
            const lastResponseText = await allMessages[allMessages.length - 1].textContent();
            console.log('\n--- Agent Response (File Analysis) ---');
            console.log(lastResponseText);
            console.log('---------------------------------------\n');
        }

        console.log('\n✅ All tests passed! The application is working correctly.');

        // Keep browser open for a few seconds to see the results
        await page.waitForTimeout(3000);

    } catch (error) {
        console.error('❌ Test failed:', error.message);
        // Take a screenshot on failure
        await page.screenshot({ path: 'd:\\Repos\\foundryAgent\\test-failure.png' });
        console.log('Screenshot saved to test-failure.png');
    } finally {
        await browser.close();
        console.log('\nBrowser closed. Test complete.');
    }
})();
