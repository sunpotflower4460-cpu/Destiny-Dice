mergeInto(LibraryManager.library, {
  $GardenPrayerField: {
    input: null,
    bounds: null,
    place: function () {
      var input = GardenPrayerField.input;
      var bounds = GardenPrayerField.bounds;
      if (!input || !bounds) return;

      var canvas = Module.canvas.getBoundingClientRect();
      var left = canvas.left + canvas.width * bounds[0];
      var top = canvas.top + canvas.height * bounds[1];
      var width = canvas.width * bounds[2];
      var height = canvas.height * bounds[3];
      var viewport = window.visualViewport;
      if (document.activeElement === input && viewport) {
        // Keep the active field above the keyboard when the visual viewport shrinks on a phone.
        top = Math.max(viewport.offsetTop + 8, Math.min(top, viewport.offsetTop + viewport.height - height - 8));
      }
      input.style.left = left + "px";
      input.style.top = top + "px";
      input.style.width = width + "px";
      input.style.height = height + "px";
      input.style.fontSize = Math.max(16, height * 0.4) + "px";
    }
  },

  GardenPrayerInputOpen__deps: ["$GardenPrayerField"],
  GardenPrayerInputOpen: function (value, placeholder, limit) {
    var input = document.createElement("input");
    input.type = "text";
    input.lang = "ja";
    input.placeholder = UTF8ToString(placeholder);
    input.setAttribute("aria-label", input.placeholder);
    input.autocomplete = "off";
    input.enterKeyHint = "done";
    input.maxLength = limit;
    input.value = UTF8ToString(value);
    input.style.cssText = "position:fixed;z-index:2;box-sizing:border-box;padding:0 10px;" +
      "border:1px solid #78654e;border-radius:6px;background:#17192e;color:#f4eedf;" +
      "font-family:Georgia,'Hiragino Mincho ProN','Yu Mincho',serif;";
    input.addEventListener("focus", function () {
      input.style.outline = "2px solid #e1bd80";
      input.style.outlineOffset = "1px";
    });
    input.addEventListener("blur", function () { input.style.outline = ""; });
    input.addEventListener("keydown", function (event) {
      event.stopPropagation();
      if (!event.isComposing && event.keyCode !== 229 && (event.key === "Enter" || event.key === "Escape")) {
        event.preventDefault();
        input.blur();
      }
    });
    // Typing belongs to the field: the game's keyboard handlers never see these keys.
    input.addEventListener("keyup", function (event) { event.stopPropagation(); });
    input.addEventListener("keypress", function (event) { event.stopPropagation(); });
    input.addEventListener("focus", GardenPrayerField.place);
    input.addEventListener("blur", GardenPrayerField.place);
    GardenPrayerField.input = input;
    document.body.appendChild(input);
    window.addEventListener("resize", GardenPrayerField.place);
    if (window.visualViewport) {
      window.visualViewport.addEventListener("resize", GardenPrayerField.place);
      window.visualViewport.addEventListener("scroll", GardenPrayerField.place);
    }
  },

  GardenPrayerInputPlace__deps: ["$GardenPrayerField"],
  GardenPrayerInputPlace: function (left, top, width, height) {
    GardenPrayerField.bounds = [left, top, width, height];
    GardenPrayerField.place();
  },

  GardenPrayerInputRead__deps: ["$GardenPrayerField", "malloc"],
  GardenPrayerInputRead: function () {
    var value = GardenPrayerField.input ? GardenPrayerField.input.value : "";
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  GardenPrayerInputClose__deps: ["$GardenPrayerField"],
  GardenPrayerInputClose: function () {
    if (!GardenPrayerField.input) return;
    window.removeEventListener("resize", GardenPrayerField.place);
    if (window.visualViewport) {
      window.visualViewport.removeEventListener("resize", GardenPrayerField.place);
      window.visualViewport.removeEventListener("scroll", GardenPrayerField.place);
    }
    GardenPrayerField.input.remove();
    GardenPrayerField.input = null;
    GardenPrayerField.bounds = null;
  }
});
