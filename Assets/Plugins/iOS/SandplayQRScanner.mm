// Native iOS QR scanner using AVFoundation (same engine as the iOS Camera app).
// Presents a full-screen UIViewController over the Unity view, scans for QR
// codes, and posts the decoded string back to Unity via UnitySendMessage.

#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

extern "C" {
    void UnitySendMessage(const char* obj, const char* method, const char* msg);
}

@interface SandplayQRScannerVC : UIViewController <AVCaptureMetadataOutputObjectsDelegate>
@property (nonatomic, strong) AVCaptureSession* session;
@property (nonatomic, strong) AVCaptureVideoPreviewLayer* previewLayer;
@property (nonatomic, strong) UIView* viewfinder;
@property (nonatomic, strong) UILabel* statusLabel;
@property (nonatomic, copy)   NSString* callbackObject;
@property (nonatomic, copy)   NSString* callbackMethod;
@property (nonatomic, assign) BOOL finished;
@end

@implementation SandplayQRScannerVC

- (BOOL)prefersStatusBarHidden { return YES; }
- (UIInterfaceOrientationMask)supportedInterfaceOrientations {
    // Match Unity's allowed orientations
    return UIInterfaceOrientationMaskAll;
}

- (void)viewDidLoad {
    [super viewDidLoad];
    self.view.backgroundColor = UIColor.blackColor;

    AVCaptureDevice* device = nil;
    if (@available(iOS 10.0, *)) {
        device = [AVCaptureDevice defaultDeviceWithDeviceType:AVCaptureDeviceTypeBuiltInWideAngleCamera
                                                    mediaType:AVMediaTypeVideo
                                                     position:AVCaptureDevicePositionBack];
    }
    if (!device) device = [AVCaptureDevice defaultDeviceWithMediaType:AVMediaTypeVideo];
    if (!device) { [self finishWithCode:@""]; return; }

    NSError* err = nil;
    AVCaptureDeviceInput* input = [AVCaptureDeviceInput deviceInputWithDevice:device error:&err];
    if (!input || err) { [self finishWithCode:@""]; return; }

    self.session = [[AVCaptureSession alloc] init];
    if ([self.session canSetSessionPreset:AVCaptureSessionPresetHigh]) {
        self.session.sessionPreset = AVCaptureSessionPresetHigh;
    }
    if ([self.session canAddInput:input]) [self.session addInput:input];

    AVCaptureMetadataOutput* output = [[AVCaptureMetadataOutput alloc] init];
    if ([self.session canAddOutput:output]) [self.session addOutput:output];
    [output setMetadataObjectsDelegate:self queue:dispatch_get_main_queue()];
    if ([output.availableMetadataObjectTypes containsObject:AVMetadataObjectTypeQRCode]) {
        output.metadataObjectTypes = @[AVMetadataObjectTypeQRCode];
    }

    // Continuous autofocus / exposure helps a lot for small codes.
    if ([device lockForConfiguration:nil]) {
        if ([device isFocusModeSupported:AVCaptureFocusModeContinuousAutoFocus])
            device.focusMode = AVCaptureFocusModeContinuousAutoFocus;
        if ([device isExposureModeSupported:AVCaptureExposureModeContinuousAutoExposure])
            device.exposureMode = AVCaptureExposureModeContinuousAutoExposure;
        if ([device respondsToSelector:@selector(isAutoFocusRangeRestrictionSupported)] &&
            device.isAutoFocusRangeRestrictionSupported) {
            device.autoFocusRangeRestriction = AVCaptureAutoFocusRangeRestrictionNear;
        }
        [device unlockForConfiguration];
    }

    self.previewLayer = [[AVCaptureVideoPreviewLayer alloc] initWithSession:self.session];
    self.previewLayer.frame = self.view.bounds;
    self.previewLayer.videoGravity = AVLayerVideoGravityResizeAspectFill;
    [self.view.layer addSublayer:self.previewLayer];

    // Dimmed overlay with a clear viewfinder hole
    [self buildOverlay];

    // Cancel button
    UIButton* cancelBtn = [UIButton buttonWithType:UIButtonTypeSystem];
    cancelBtn.translatesAutoresizingMaskIntoConstraints = NO;
    [cancelBtn setTitle:@"×" forState:UIControlStateNormal];
    cancelBtn.accessibilityLabel = @"Close scanner";
    [cancelBtn setTitleColor:UIColor.whiteColor forState:UIControlStateNormal];
    cancelBtn.titleLabel.font = [UIFont systemFontOfSize:32 weight:UIFontWeightSemibold];
    cancelBtn.backgroundColor = [UIColor colorWithWhite:1.0 alpha:0.18];
    cancelBtn.layer.cornerRadius = 10;
    [cancelBtn addTarget:self action:@selector(onCancel) forControlEvents:UIControlEventTouchUpInside];
    [self.view addSubview:cancelBtn];
    [NSLayoutConstraint activateConstraints:@[
        [cancelBtn.topAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.topAnchor constant:16],
        [cancelBtn.trailingAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.trailingAnchor constant:-16],
        [cancelBtn.widthAnchor constraintEqualToConstant:48],
        [cancelBtn.heightAnchor constraintEqualToConstant:48]
    ]];

    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        [self.session startRunning];
    });
}

- (void)buildOverlay {
    CGFloat side = MIN(self.view.bounds.size.width, self.view.bounds.size.height) * 0.65;
    CGRect hole = CGRectMake((self.view.bounds.size.width - side) / 2.0,
                             (self.view.bounds.size.height - side) / 2.0,
                             side, side);

    UIView* dim = [[UIView alloc] initWithFrame:self.view.bounds];
    dim.backgroundColor = [UIColor colorWithWhite:0 alpha:0.55];
    dim.userInteractionEnabled = NO;
    dim.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;

    CAShapeLayer* mask = [CAShapeLayer layer];
    UIBezierPath* path = [UIBezierPath bezierPathWithRect:dim.bounds];
    [path appendPath:[[UIBezierPath bezierPathWithRoundedRect:hole cornerRadius:12] bezierPathByReversingPath]];
    mask.path = path.CGPath;
    mask.fillRule = kCAFillRuleEvenOdd;
    dim.layer.mask = mask;
    [self.view addSubview:dim];

    self.viewfinder = [[UIView alloc] initWithFrame:hole];
    self.viewfinder.layer.borderColor = [UIColor colorWithRed:0.3 green:0.8 blue:1.0 alpha:1.0].CGColor;
    self.viewfinder.layer.borderWidth = 2.0;
    self.viewfinder.layer.cornerRadius = 12;
    self.viewfinder.userInteractionEnabled = NO;
    [self.view addSubview:self.viewfinder];

    self.statusLabel = [[UILabel alloc] initWithFrame:CGRectMake(0, hole.origin.y + side + 20,
                                                                  self.view.bounds.size.width, 24)];
    self.statusLabel.textAlignment = NSTextAlignmentCenter;
    self.statusLabel.textColor = [UIColor colorWithWhite:0.9 alpha:1.0];
    self.statusLabel.font = [UIFont systemFontOfSize:15];
    self.statusLabel.text = @"Point camera at a QR code";
    [self.view addSubview:self.statusLabel];
}

- (void)viewDidLayoutSubviews {
    [super viewDidLayoutSubviews];
    self.previewLayer.frame = self.view.bounds;

    AVCaptureConnection* conn = self.previewLayer.connection;
    if (conn.isVideoOrientationSupported) {
        UIInterfaceOrientation o;
        if (@available(iOS 13.0, *)) {
            o = self.view.window.windowScene.interfaceOrientation;
        } else {
            o = [UIApplication sharedApplication].statusBarOrientation;
        }
        AVCaptureVideoOrientation vo = AVCaptureVideoOrientationPortrait;
        switch (o) {
            case UIInterfaceOrientationLandscapeLeft:       vo = AVCaptureVideoOrientationLandscapeLeft; break;
            case UIInterfaceOrientationLandscapeRight:      vo = AVCaptureVideoOrientationLandscapeRight; break;
            case UIInterfaceOrientationPortraitUpsideDown:  vo = AVCaptureVideoOrientationPortraitUpsideDown; break;
            case UIInterfaceOrientationPortrait:            vo = AVCaptureVideoOrientationPortrait; break;
            default: break;
        }
        conn.videoOrientation = vo;
    }
}

- (void)captureOutput:(AVCaptureOutput*)output
didOutputMetadataObjects:(NSArray<__kindof AVMetadataObject*>*)metadataObjects
       fromConnection:(AVCaptureConnection*)connection {
    if (self.finished) return;
    for (AVMetadataObject* obj in metadataObjects) {
        if ([obj isKindOfClass:[AVMetadataMachineReadableCodeObject class]]) {
            AVMetadataMachineReadableCodeObject* code = (AVMetadataMachineReadableCodeObject*)obj;
            NSString* s = code.stringValue;
            if (s.length > 0) {
                [self finishWithCode:s];
                return;
            }
        }
    }
}

- (void)onCancel { [self finishWithCode:@""]; }

- (void)finishWithCode:(NSString*)code {
    if (self.finished) return;
    self.finished = YES;

    if (self.session.isRunning) [self.session stopRunning];

    NSString* obj = self.callbackObject ?: @"";
    NSString* mtd = self.callbackMethod ?: @"";
    NSString* payload = code ?: @"";

    [self dismissViewControllerAnimated:YES completion:^{
        if (obj.length > 0 && mtd.length > 0) {
            UnitySendMessage([obj UTF8String], [mtd UTF8String], [payload UTF8String]);
        }
    }];
}

@end

static SandplayQRScannerVC* g_currentVC = nil;

static UIViewController* _SandplayQR_TopVC() {
    UIWindow* window = nil;
    if (@available(iOS 13.0, *)) {
        for (UIScene* scene in [UIApplication sharedApplication].connectedScenes) {
            if ([scene isKindOfClass:[UIWindowScene class]] &&
                scene.activationState == UISceneActivationStateForegroundActive) {
                for (UIWindow* w in ((UIWindowScene*)scene).windows) {
                    if (w.isKeyWindow) { window = w; break; }
                }
                if (window) break;
            }
        }
    }
    if (!window) window = [UIApplication sharedApplication].keyWindow;
    UIViewController* root = window.rootViewController;
    while (root.presentedViewController) root = root.presentedViewController;
    return root;
}

extern "C" {

void _SandplayQR_Start(const char* callbackObject, const char* callbackMethod) {
    if (g_currentVC) return; // already presenting
    NSString* obj = callbackObject ? [NSString stringWithUTF8String:callbackObject] : @"";
    NSString* mtd = callbackMethod ? [NSString stringWithUTF8String:callbackMethod] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* top = _SandplayQR_TopVC();
        if (!top) return;
        SandplayQRScannerVC* vc = [[SandplayQRScannerVC alloc] init];
        vc.callbackObject = obj;
        vc.callbackMethod = mtd;
        vc.modalPresentationStyle = UIModalPresentationFullScreen;
        g_currentVC = vc;
        [top presentViewController:vc animated:YES completion:nil];
        // Clear the global once dismissed
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)),
                       dispatch_get_main_queue(), ^{
            // no-op; actual clearing happens in _SandplayQR_Stop or finishWithCode dismissal
        });
    });
}

void _SandplayQR_Stop() {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (g_currentVC) {
            [g_currentVC onCancel];
            g_currentVC = nil;
        }
    });
}

}
