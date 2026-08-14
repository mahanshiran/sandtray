mergeInto(LibraryManager.library, {

  // Called from NativeShare.cs on WebGL builds.
  // Creates an in-memory Blob and triggers a browser "Save As" download.
  _SaveFileBytes: function(fileNamePtr, bytesPtr, length, mimeTypePtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var mimeType = mimeTypePtr ? UTF8ToString(mimeTypePtr) : "application/octet-stream";
    if (!mimeType) mimeType = "application/octet-stream";

    var data = new Uint8Array(HEAPU8.buffer, bytesPtr, length);
    var copy = new Uint8Array(data);

    var blob = new Blob([copy], { type: mimeType });
    var url  = URL.createObjectURL(blob);

    var anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);

    setTimeout(function() { URL.revokeObjectURL(url); }, 10000);
  }

});
