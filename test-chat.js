const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

function envFlag(name, defaultValue) {
    const raw = process.env[name];
    if (raw === undefined || raw === null || raw === '') return defaultValue;
    return !/^false$/i.test(String(raw).trim());
}

function ensureDir(dirPath) {
    if (!fs.existsSync(dirPath)) {
        fs.mkdirSync(dirPath, { recursive: true });
    }
}

function nowStamp() {
    const d = new Date();
    const pad = (n) => String(n).padStart(2, '0');
    return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}-${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}`;
}

async function safeScreenshot(page, artifactsDir, name) {
    try {
        ensureDir(artifactsDir);
        const file = path.join(artifactsDir, `${name}-${nowStamp()}.png`);
        await page.screenshot({ path: file, fullPage: true });
        console.log(`[artifact] screenshot: ${file}`);
    } catch (e) {
        console.log(`[artifact] failed to screenshot: ${e && e.message ? e.message : e}`);
    }
}

async function getLastAgentMessageText(page) {
    const msgs = await page.locator('.message.agent .message-content').allTextContents();
    if (!msgs || msgs.length === 0) return '';
    return (msgs[msgs.length - 1] || '').trim();
}

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

function looksLikeMissingMcpToken(text) {
    const t = (text || '').toLowerCase();
    return t.includes('missing copilot mcp auth token') || t.includes('copilot_mcp_token') || t.includes('github_copilot_mcp_token');
}

function looksLikeRepoResults(text) {
    if (!text) return false;
    // Accept either raw JSON from MCP gateway or a formatted list with GitHub URLs.
    return /\bfull_name\b/i.test(text)
        || /\bhtml_url\b/i.test(text)
        || /https:\/\/github\.com\//i.test(text);
}

async function waitForChatIdle(page, timeoutMs) {
    await page.waitForSelector('#loadingIndicator', { state: 'attached', timeout: 5000 });
    await page.waitForSelector('#loadingIndicator', { state: 'detached', timeout: timeoutMs });
}

async function sendChatMessage(page, message, { timeoutMs = 120000 } = {}) {
    await page.waitForSelector('#messageInput', { state: 'visible', timeout: 10000 });

    const beforeAgentCount = await page.locator('.message.agent .message-content').count();
    await page.fill('#messageInput', message);
    await page.click('#sendButton');
    await waitForChatIdle(page, timeoutMs);

    const errorBanner = page.locator('.error-message');
    if (await errorBanner.count()) {
        const errText = (await errorBanner.first().textContent()) || 'Unknown UI error';
        throw new Error(`UI reported an error after sending message: ${errText.trim()}`);
    }

    // Ensure a new agent message arrived.
    await page.waitForFunction(
        ({ selector, expectedMin }) => document.querySelectorAll(selector).length >= expectedMin,
        { selector: '.message.agent .message-content', expectedMin: beforeAgentCount + 1 },
        { timeout: 15000 }
    );

    const last = await getLastAgentMessageText(page);
    console.log(`[agent:last] ${(last || '').slice(0, 280).replace(/\s+/g, ' ')}`);
    return last;
}

async function openApp(page, baseUrl) {
    console.log(`Navigating to ${baseUrl}`);
    await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('#messageInput', { timeout: 10000 });
}

async function testMcpPanelSmoke(page) {
    console.log('\n=== A) MCP panel smoke test ===');

    const [discoverResponse] = await Promise.all([
        page.waitForResponse((r) => r.url().includes('/api/mcp/discover'), { timeout: 15000 }),
        page.click('#mcpButton')
    ]);

    console.log(`[api] /api/mcp/discover => ${discoverResponse.status()}`);
    assert(discoverResponse.status() === 200, `Expected /api/mcp/discover 200, got ${discoverResponse.status()}`);

    await page.waitForSelector('#mcpPanel.show', { timeout: 10000 });
    await page.waitForSelector('.mcp-tool', { timeout: 10000 });

    const toolNames = (await page.locator('.mcp-tool-name').allTextContents()).map(t => (t || '').trim());
    console.log(`[mcp] tools (${toolNames.length}): ${toolNames.join(', ')}`);
    assert(toolNames.length > 0, 'Expected at least one MCP tool card');
    assert(toolNames.includes('search_repositories'), 'Expected MCP tools to include a tool named exactly "search_repositories"');
}

async function testListReposViaMcpTool(page) {
    console.log('\n=== B) “List repos” via MCP tool through the UI ===');

    const prompt1 = 'Use the search_repositories tool with query "user:sayedimac" and return top 10 repositories with name and url. IMPORTANT: include a full https://github.com/... URL for each repo (e.g., html_url).';
    let last = await sendChatMessage(page, prompt1, { timeoutMs: 120000 });

    if (looksLikeMissingMcpToken(last)) {
        throw new Error('MCP tool execution failed due to missing token. Set COPILOT_MCP_TOKEN (or GITHUB_COPILOT_MCP_TOKEN) before running UI tests.');
    }

    if (!looksLikeRepoResults(last)) {
        const prompt2 = 'Re-run the search_repositories tool with query "user:sayedimac" and output ONLY a newline-separated list of 10 lines, each formatted exactly as: <repo_name> - <repo_url>. The URL must be a full https://github.com/... link.';
        last = await sendChatMessage(page, prompt2, { timeoutMs: 120000 });
    }

    assert(looksLikeRepoResults(last), 'Expected repo results in agent response (look for "full_name", "html_url", or https://github.com/ links)');
}

async function testAdditionalMcpQuestions(page) {
    console.log('\n=== C) Additional MCP-driven questions ===');

    const q1 = 'Use get_file_contents to fetch README.md from owner "sayedimac", repo "foundryAgent", path "README.md". Summarize in exactly 3 markdown bullet lines, each starting with "- ".';
    let a1 = await sendChatMessage(page, q1, { timeoutMs: 120000 });
    if (looksLikeMissingMcpToken(a1)) {
        throw new Error('MCP tool execution failed due to missing token. Set COPILOT_MCP_TOKEN (or GITHUB_COPILOT_MCP_TOKEN) before running UI tests.');
    }

    const listLineRegex = /^([-*•]\s+|\d+\.[\s]+)/;
    // List assertion: require at least 3 list lines (agents sometimes add a leading sentence).
    let listLines = (a1 || '').split(/\r?\n/).map(l => l.trim()).filter(l => listLineRegex.test(l));
    if (listLines.length < 3) {
        const q1Retry = 'Please reformat your previous answer as exactly 3 markdown bullet lines. Output ONLY 3 lines and each line must start with "- ".';
        a1 = await sendChatMessage(page, q1Retry, { timeoutMs: 120000 });
        listLines = (a1 || '').split(/\r?\n/).map(l => l.trim()).filter(l => listLineRegex.test(l));
    }
    if (listLines.length < 3) {
        const q1Retry2 = 'Output ONLY 3 lines, formatted exactly as a numbered list: "1. ..." then "2. ..." then "3. ...". No extra text.';
        a1 = await sendChatMessage(page, q1Retry2, { timeoutMs: 120000 });
        listLines = (a1 || '').split(/\r?\n/).map(l => l.trim()).filter(l => listLineRegex.test(l));
    }

    if (listLines.length < 3) {
        const clauses = (a1 || '')
            .split(/[\r\n]+|\.(?=\s)|;(?=\s)/)
            .map(s => s.trim())
            .filter(s => s.length >= 20);
        assert(clauses.length >= 3, 'Expected the README.md summary to contain at least 3 distinct points');
    }

    const q2 = 'Use list_issues for owner "sayedimac", repo "foundryAgent". If none, say "No issues found".';
    const a2 = await sendChatMessage(page, q2, { timeoutMs: 120000 });
    if (looksLikeMissingMcpToken(a2)) {
        throw new Error('MCP tool execution failed due to missing token. Set COPILOT_MCP_TOKEN (or GITHUB_COPILOT_MCP_TOKEN) before running UI tests.');
    }
    assert(/no issues found/i.test(a2) || /#\d+/.test(a2) || /\bissue\b/i.test(a2), 'Expected either issues listed or "No issues found"');
}

async function testMarkdownRendering(page) {
    console.log('\n=== D1) Markdown rendering request ===');
    let a = await sendChatMessage(
        page,
        'Return a short markdown response with (1) a bullet list and (2) a markdown table written with pipe characters. The table must have a header row and separator row, and exactly 2 data rows about fruits.',
        { timeoutMs: 120000 }
    );

    if (!/\|/.test(a)) {
        a = await sendChatMessage(
            page,
            'Reformat your previous answer as markdown only. Output a pipe-based markdown table with a header row, separator row, and exactly 2 fruit rows. Include at least one "|" character per row.',
            { timeoutMs: 120000 }
        );
    }

    assert(/\|/.test(a), 'Expected markdown output containing a pipe-based table');
}

async function testFileUploadFlow(page, repoRoot) {
    console.log('\n=== D2) File upload flow ===');
    await page.click('#attachButton');

    const csvPath = path.join(repoRoot, 'demo-sales-data.csv');
    assert(fs.existsSync(csvPath), `Expected demo file at ${csvPath}`);

    const fileInput = page.locator('#fileInput');
    await fileInput.setInputFiles(csvPath);
    await page.waitForSelector('.attached-file', { timeout: 5000 });

    const a = await sendChatMessage(page, 'Analyze this sales data and tell me which product had the highest total revenue.', { timeoutMs: 180000 });
    assert(a && a.length > 0, 'Expected a non-empty response for file analysis');
}

(async () => {
    console.log('Starting Playwright UI tests...');

    const baseUrl = process.env.BASE_URL || 'http://localhost:5109';
    const headless = envFlag('HEADLESS', true);
    const artifactsDir = path.join(process.cwd(), 'test-artifacts');

    console.log(`Base URL: ${baseUrl}`);
    console.log(`Headless: ${headless} (set HEADLESS=false to show browser)`);

    const browser = await chromium.launch({ headless });
    const context = await browser.newContext();
    const page = await context.newPage();

    // Developer-friendly logging
    page.on('console', (msg) => console.log(`[browser console][${msg.type()}] ${msg.text()}`));
    page.on('pageerror', (err) => console.log(`[browser pageerror] ${err.message}`));
    page.on('requestfailed', (req) => {
        const failure = req.failure();
        console.log(`[request failed] ${req.method()} ${req.url()} :: ${failure ? failure.errorText : 'unknown'}`);
    });
    page.on('response', async (res) => {
        const url = res.url();
        if (url.includes('/api/mcp/discover') || url.includes('/api/chat') || url.includes('/api/chat/upload')) {
            console.log(`[response] ${res.status()} ${res.request().method()} ${url}`);
        }
    });

    try {
        await openApp(page, baseUrl);

        await testMcpPanelSmoke(page);
        await testListReposViaMcpTool(page);
        await testAdditionalMcpQuestions(page);
        await testMarkdownRendering(page);
        await testFileUploadFlow(page, process.cwd());

        console.log('\n✅ All UI tests passed.');
        process.exitCode = 0;
    } catch (error) {
        const msg = error && error.message ? error.message : String(error);
        console.error(`\n❌ UI test failed: ${msg}`);
        console.error(`[agent:last] ${(await getLastAgentMessageText(page)).slice(0, 400).replace(/\s+/g, ' ')}`);
        await safeScreenshot(page, artifactsDir, 'failure');
        process.exitCode = 1;
    } finally {
        await browser.close();
        console.log('Browser closed.');
    }
})();
