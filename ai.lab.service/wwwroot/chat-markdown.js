// Markdown rendering module for chat messages
// Uses Marked.js for markdown parsing and Highlight.js for code syntax highlighting

window.AILabMarkdown = (function() {
    let isInitialized = false;

    function init() {
        if (isInitialized) return;
        
        // Configure Marked.js
        if (typeof marked !== 'undefined') {
            marked.setOptions({
                breaks: true,        // Convert \n to <br>
                gfm: true,          // GitHub Flavored Markdown
                headerIds: false,   // Don't add IDs to headers (security)
                mangle: false,      // Don't mangle email addresses
                sanitize: false,    // We'll use DOMPurify if needed
                highlight: function(code, lang) {
                    // Use Highlight.js for code blocks if available
                    if (typeof hljs !== 'undefined' && lang && hljs.getLanguage(lang)) {
                        try {
                            return hljs.highlight(code, { language: lang }).value;
                        } catch (err) {
                            console.warn('Highlight.js error:', err);
                        }
                    }
                    return code; // Return plain code if highlighting fails
                }
            });
        }
        
        isInitialized = true;
        console.log('✅ AILabMarkdown initialized');
    }

    function renderMarkdown(text) {
        if (!text) return '';
        
        init(); // Ensure initialized
        
        if (typeof marked === 'undefined') {
            console.warn('Marked.js not loaded, returning plain text');
            return escapeHtml(text);
        }
        
        try {
            // Parse markdown to HTML
            const html = marked.parse(text);
            return html;
        } catch (err) {
            console.error('Markdown parsing error:', err);
            return escapeHtml(text);
        }
    }

    function updateElement(elementId, markdownText) {
        const el = document.getElementById(elementId);
        if (!el) {
            console.warn('Element not found:', elementId);
            return;
        }
        
        const html = renderMarkdown(markdownText);
        el.innerHTML = html;
        
        // Apply syntax highlighting to code blocks if Highlight.js is available
        if (typeof hljs !== 'undefined') {
            el.querySelectorAll('pre code:not(.hljs)').forEach((block) => {
                hljs.highlightElement(block);
            });
        }
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Batch render multiple messages (for initial load)
    function renderMessages(messages) {
        messages.forEach(msg => {
            updateElement(msg.elementId, msg.markdown);
        });
    }

    return {
        init,
        renderMarkdown,
        updateElement,
        renderMessages
    };
})();

// Auto-initialize on load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function() {
        AILabMarkdown.init();
    });
} else {
    AILabMarkdown.init();
}
