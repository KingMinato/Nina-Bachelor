mergeInto(LibraryManager.library, {
  RedirectTo: function (urlPtr) {
    window.location.href = UTF8ToString(urlPtr);
  }
});
