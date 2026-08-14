#import <UIKit/UIKit.h>

extern "C" {

void _ShareFile(const char* path, const char* mimeType, const char* title)
{
    NSString* filePath = [NSString stringWithUTF8String:path];
    NSURL* fileURL = [NSURL fileURLWithPath:filePath];

    if (![[NSFileManager defaultManager] fileExistsAtPath:filePath]) {
        NSLog(@"[NativeShare] File not found: %@", filePath);
        return;
    }

    UIActivityViewController* activityVC =
        [[UIActivityViewController alloc]
            initWithActivityItems:@[fileURL]
            applicationActivities:nil];

    // On iPad the popover needs an anchor; use the centre of the root view
    UIViewController* rootVC =
        [UIApplication sharedApplication].keyWindow.rootViewController;
    while (rootVC.presentedViewController)
        rootVC = rootVC.presentedViewController;

    if (UI_USER_INTERFACE_IDIOM() == UIUserInterfaceIdiomPad) {
        activityVC.popoverPresentationController.sourceView = rootVC.view;
        activityVC.popoverPresentationController.sourceRect =
            CGRectMake(CGRectGetMidX(rootVC.view.bounds),
                       CGRectGetMidY(rootVC.view.bounds), 0, 0);
        activityVC.popoverPresentationController.permittedArrowDirections = 0;
    }

    [rootVC presentViewController:activityVC animated:YES completion:nil];
}

} // extern "C"
