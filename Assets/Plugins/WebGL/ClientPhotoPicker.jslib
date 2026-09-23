mergeInto(LibraryManager.library, {
  SandtrayPickClientPhoto: function(receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.style.display = 'none';
    document.body.appendChild(input);
    var finished = false;
    function finish(value) {
      if (finished) return;
      finished = true;
      input.remove();
      SendMessage(receiver, 'OnBrowserPhoto', value);
    }
    input.addEventListener('cancel', function() { finish(''); });
    input.onchange = function() {
      var file = input.files && input.files[0];
      if (!file) { finish(''); return; }
      if (file.size > 20 * 1024 * 1024) { finish('error'); return; }
      var url = URL.createObjectURL(file);
      var img = new Image();
      img.onload = function() {
        try {
          var canvas = document.createElement('canvas');
          canvas.width = canvas.height = 256;
          var side = Math.min(img.width, img.height);
          canvas.getContext('2d').drawImage(img, (img.width-side)/2, (img.height-side)/2,
            side, side, 0, 0, 256, 256);
          finish(canvas.toDataURL('image/png').split(',')[1]);
        } catch(e) { finish('error'); }
        URL.revokeObjectURL(url);
      };
      img.onerror = function() { URL.revokeObjectURL(url); finish('error'); };
      img.src = url;
    };
    input.click();
  }
});
