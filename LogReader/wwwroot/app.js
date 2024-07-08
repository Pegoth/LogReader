/**
 * Loads all bootstrap tooltips on the current page.
 */
function loadTooltips() {
    $("[data-bs-toggle='tooltip']").tooltip({
        trigger: "hover"
    });
}

/**
 * Updates all bootstrap tooltips on the current page.
 */
function updateTooltips() {
    $("[data-bs-toggle='tooltip']").each(function() {
        const t = $(this);
        const title = t.attr("title");
        if (title != null && title !== "") {
            t.attr("data-bs-original-title", t.attr("title"));
            t.attr("aria-label", t.attr("title"));
            t.attr("title", null);
        }
    });
}

/**
 * Sets the clipboard to the given data.
 * @param {string} data The data to set
 */
function setClipboard(data) {
    navigator.clipboard.writeText(data)
        .catch(err => console.error(err));
}

/**
 * Gets the current value of the clipboard.
 */
function getClipboard() {
    return navigator.clipboard.readText();
}