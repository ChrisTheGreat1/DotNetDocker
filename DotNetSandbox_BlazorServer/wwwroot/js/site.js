function addTextAreaEventListener() {
    const textarea = document.querySelector('#code-text-area');

    textarea.addEventListener('keydown', event => {
        if (event.key === 'Tab') {
            document.execCommand('insertText', false, '\t');
            event.preventDefault();
        }
    });
}