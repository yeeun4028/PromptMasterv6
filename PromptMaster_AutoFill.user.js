// ==UserScript==
// @name         PromptMaster Auto-Fill (Universal AI)
// @namespace    http://tampermonkey.net/
// @version      1.2
// @description  自动将 URL 中的 ?q= 参数填入 DeepSeek/Gemini/AI Studio/GLM/Qwen/Doubao 的输入框并发送。
// @author       PromptMaster
// @match        *://chat.deepseek.com/*
// @match        *://gemini.google.com/*
// @match        *://aistudio.google.com/*
// @match        *://chatglm.cn/*
// @match        *://tongyi.aliyun.com/*
// @match        *://www.doubao.com/*
// @grant        none
// @run-at       document-end
// ==/UserScript==

(function () {
    'use strict';

    // 1. 获取并解码 URL 参数 q=
    const urlParams = new URLSearchParams(window.location.search);
    const prompt = urlParams.get('q');

    if (!prompt) return;

    console.log('[PromptMaster] Detected prompt:', prompt);

    // 2. 深度查找函数 (支持 Shadow DOM)
    function findDeep(selector, root = document) {
        // 1. 在当前根节点查找
        const el = root.querySelector(selector);
        if (el) return el;

        // 2. 遍历所有子节点查找 Shadow Root
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null, false);
        let node;
        while (node = walker.nextNode()) {
            if (node.shadowRoot) {
                const found = findDeep(selector, node.shadowRoot);
                if (found) return found;
            }
        }
        return null;
    }

    // 3. 通用 React/Vue 输入框赋值函数 (核心)
    function setNativeValue(element, value) {
        const valueSetter = Object.getOwnPropertyDescriptor(element, 'value').set;
        const prototype = Object.getPrototypeOf(element);
        const prototypeValueSetter = Object.getOwnPropertyDescriptor(prototype, 'value').set;

        if (prototypeValueSetter && valueSetter !== prototypeValueSetter) {
            prototypeValueSetter.call(element, value);
        } else if (valueSetter) {
            valueSetter.call(element, value);
        } else {
            element.value = value;
        }

        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // 4. 重试逻辑：等待页面加载并找到输入框
    let retryCount = 0;
    const maxRetries = 40; // 尝试 40 次，每次 500ms (共 20秒)

    const timer = setInterval(() => {
        retryCount++;
        if (retryCount > maxRetries) {
            console.log('[PromptMaster] Timeout searching for input.');
            clearInterval(timer);
            return;
        }

        // 查找输入框 (优先匹配 textarea)
        let inputEl = null;

        const isGoogleAI = window.location.hostname.includes('gemini.google.com') || window.location.hostname.includes('aistudio.google.com');

        if (isGoogleAI) {
            // Gemini / AI Studio 专用逻辑 (尝试深度查找)
            inputEl = findDeep('rich-textarea div[contenteditable="true"]');
            if (!inputEl) inputEl = findDeep('div[role="textbox"]');
            if (!inputEl) inputEl = findDeep('div[contenteditable="true"]');
            if (!inputEl) inputEl = findDeep('textarea');
        } else {
            // 其他站点通用逻辑
            inputEl = document.querySelector('textarea');
            if (!inputEl) inputEl = document.querySelector('input[type="text"]');
            if (!inputEl) inputEl = document.querySelector('#chat-input');
        }

        if (inputEl) {
            clearInterval(timer);
            console.log('[PromptMaster] Input element found:', inputEl);

            // 1. 聚焦
            inputEl.focus();
            inputEl.click();

            // 2. 填入内容
            if (isGoogleAI) {
                // Gemini / AI Studio 必须用 execCommand 才能模拟真实输入，否则发送按钮可能不亮
                document.execCommand('insertText', false, prompt);
            }
            else if (inputEl.tagName.toLowerCase() === 'div' || inputEl.isContentEditable) {
                inputEl.textContent = prompt;
                inputEl.dispatchEvent(new Event('input', { bubbles: true }));
            } else {
                setNativeValue(inputEl, prompt);
            }

            // 3. 延迟发送
            setTimeout(() => {
                let btn = null;
                if (isGoogleAI) {
                    btn = findDeep('button[aria-label*="Send"]'); // EN
                    if (!btn) btn = findDeep('button[aria-label*="发送"]'); // CN
                    if (!btn) btn = findDeep('button.send-button'); // Fallback class
                    if (!btn) btn = findDeep('button[aria-label*="Run"]'); // AI Studio
                    // AI Studio Run button might be different, trying common SVG icon class or id
                    if (!btn) btn = findDeep('button#run-button');
                } else {
                    btn = document.querySelector('button[aria-label*="Send"]');
                    if (!btn) btn = document.querySelector('button[aria-label*="发送"]');
                    if (!btn) btn = document.querySelector('div[role="button"][aria-label*="Send"]');
                    if (!btn) btn = document.querySelector('.enter-btn');
                }

                if (btn) {
                    console.log('[PromptMaster] Clicking send button:', btn);
                    btn.click();
                } else {
                    // Gemini 通常需要点击按钮，模拟 Enter 可能只是换行
                    console.log('[PromptMaster] No send button found, trying Enter key...');
                    const enterEvent = new KeyboardEvent('keydown', {
                        bubbles: true, cancelable: true, keyCode: 13, which: 13, key: 'Enter'
                    });
                    inputEl.dispatchEvent(enterEvent);
                }

                // 清除 URL 参数
                window.history.replaceState({}, document.title, window.location.pathname);

            }, 800);
        }
    }, 500);

})();
