// ==UserScript==
// @name         PromptMaster Auto-Fill (Universal AI)
// @namespace    http://tampermonkey.net/
// @version      1.0
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

    // 2. 通用 React/Vue 输入框赋值函数 (核心)
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

    // 3. 重试逻辑：等待页面加载并找到输入框
    let retryCount = 0;
    const maxRetries = 20; // 尝试 20 次，每次 500ms

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
            // Gemini / AI Studio 专用逻辑
            inputEl = document.querySelector('rich-textarea div[contenteditable="true"]');
            if (!inputEl) inputEl = document.querySelector('div[role="textbox"]');
            if (!inputEl) inputEl = document.querySelector('div[contenteditable="true"]');
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
                    btn = document.querySelector('button[aria-label*="Send"]'); // EN
                    if (!btn) btn = document.querySelector('button[aria-label*="发送"]'); // CN
                    if (!btn) btn = document.querySelector('button.send-button'); // Fallback class
                    if (!btn) btn = document.querySelector('button[aria-label*="Run"]'); // AI Studio
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
