mergeInto(LibraryManager.library, {
  // replace() instead of href: the game is not kept in the history, so "back" in Survey B
  // cannot lead into a second run.
  RedirectTo: function (urlPtr) {
    window.location.replace(UTF8ToString(urlPtr));
  }
});
