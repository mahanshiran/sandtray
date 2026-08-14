#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

static AVAssetWriter* gWriter = nil;
static AVAssetWriterInput* gVideoInput = nil;
static AVAssetWriterInputPixelBufferAdaptor* gAdaptor = nil;
static int gWidth = 0;
static int gHeight = 0;
static int gFps = 15;
static int gFrameIndex = 0;
static BOOL gSessionStarted = NO;

static void VideoEncoderCleanup(void)
{
    gWriter = nil;
    gVideoInput = nil;
    gAdaptor = nil;
    gWidth = gHeight = 0;
    gFrameIndex = 0;
    gSessionStarted = NO;
}

extern "C" {

int _VideoEncoder_Start(const char* path, int width, int height, int fps)
{
    VideoEncoderCleanup();

    if (!path || width <= 0 || height <= 0) return 0;
    gWidth = width;
    gHeight = height;
    gFps = fps > 0 ? fps : 15;
    gFrameIndex = 0;

    NSString* filePath = [NSString stringWithUTF8String:path];
    NSURL* fileURL = [NSURL fileURLWithPath:filePath];
    [[NSFileManager defaultManager] removeItemAtURL:fileURL error:nil];

    NSError* error = nil;
    gWriter = [[AVAssetWriter alloc] initWithURL:fileURL fileType:AVFileTypeMPEG4 error:&error];
    if (!gWriter || error) {
        NSLog(@"[VideoEncoder] Writer create failed: %@", error);
        VideoEncoderCleanup();
        return 0;
    }

    NSDictionary* settings = @{
        AVVideoCodecKey: AVVideoCodecTypeH264,
        AVVideoWidthKey: @(width),
        AVVideoHeightKey: @(height),
        AVVideoCompressionPropertiesKey: @{
            AVVideoAverageBitRateKey: @(width * height * 4),
            AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel
        }
    };

    gVideoInput = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo
                                                     outputSettings:settings];
    gVideoInput.expectsMediaDataInRealTime = YES;

    NSDictionary* attrs = @{
        (NSString*)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (NSString*)kCVPixelBufferWidthKey: @(width),
        (NSString*)kCVPixelBufferHeightKey: @(height)
    };
    gAdaptor = [AVAssetWriterInputPixelBufferAdaptor
                assetWriterInputPixelBufferAdaptorWithAssetWriterInput:gVideoInput
                sourcePixelBufferAttributes:attrs];

    if (![gWriter canAddInput:gVideoInput]) {
        NSLog(@"[VideoEncoder] Cannot add video input");
        VideoEncoderCleanup();
        return 0;
    }
    [gWriter addInput:gVideoInput];

    if (![gWriter startWriting]) {
        NSLog(@"[VideoEncoder] startWriting failed: %@", gWriter.error);
        VideoEncoderCleanup();
        return 0;
    }
    [gWriter startSessionAtSourceTime:kCMTimeZero];
    gSessionStarted = YES;
    NSLog(@"[VideoEncoder] Started %dx%d @ %dfps → %@", width, height, gFps, filePath);
    return 1;
}

int _VideoEncoder_AddFrameRGB(const unsigned char* rgb, int length, int width, int height)
{
    if (!gSessionStarted || !gWriter || !gVideoInput || !gAdaptor || !rgb) return 0;
    if (width != gWidth || height != gHeight) return 0;
    if (length < width * height * 3) return 0;

    if (!gVideoInput.readyForMoreMediaData) {
        // Spin briefly — encoder may be busy
        NSDate* until = [NSDate dateWithTimeIntervalSinceNow:0.5];
        while (!gVideoInput.readyForMoreMediaData && [until timeIntervalSinceNow] > 0) {
            [NSThread sleepForTimeInterval:0.002];
        }
        if (!gVideoInput.readyForMoreMediaData) {
            NSLog(@"[VideoEncoder] Dropping frame %d — input not ready", gFrameIndex);
            return 0;
        }
    }

    CVPixelBufferRef pixelBuffer = NULL;
    CVReturn cvErr = CVPixelBufferPoolCreatePixelBuffer(
        NULL, gAdaptor.pixelBufferPool, &pixelBuffer);
    if (cvErr != kCVReturnSuccess || !pixelBuffer) {
        // Fallback allocate
        NSDictionary* attrs = @{
            (NSString*)kCVPixelBufferCGImageCompatibilityKey: @YES,
            (NSString*)kCVPixelBufferCGBitmapContextCompatibilityKey: @YES
        };
        cvErr = CVPixelBufferCreate(kCFAllocatorDefault, width, height,
                                    kCVPixelFormatType_32BGRA,
                                    (__bridge CFDictionaryRef)attrs,
                                    &pixelBuffer);
        if (cvErr != kCVReturnSuccess || !pixelBuffer) {
            NSLog(@"[VideoEncoder] Pixel buffer create failed: %d", cvErr);
            return 0;
        }
    }

    CVPixelBufferLockBaseAddress(pixelBuffer, 0);
    uint8_t* dst = (uint8_t*)CVPixelBufferGetBaseAddress(pixelBuffer);
    size_t bytesPerRow = CVPixelBufferGetBytesPerRow(pixelBuffer);

    for (int y = 0; y < height; y++) {
        // Unity ReadPixels is bottom-up; flip vertically for video
        const unsigned char* srcRow = rgb + ((height - 1 - y) * width * 3);
        uint8_t* dstRow = dst + y * bytesPerRow;
        for (int x = 0; x < width; x++) {
            dstRow[x * 4 + 0] = srcRow[x * 3 + 2]; // B
            dstRow[x * 4 + 1] = srcRow[x * 3 + 1]; // G
            dstRow[x * 4 + 2] = srcRow[x * 3 + 0]; // R
            dstRow[x * 4 + 3] = 255;               // A
        }
    }
    CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);

    CMTime t = CMTimeMake(gFrameIndex, gFps);
    BOOL ok = [gAdaptor appendPixelBuffer:pixelBuffer withPresentationTime:t];
    CVPixelBufferRelease(pixelBuffer);
    if (!ok) {
        NSLog(@"[VideoEncoder] appendPixelBuffer failed: %@", gWriter.error);
        return 0;
    }
    gFrameIndex++;
    return 1;
}

int _VideoEncoder_Finish(void)
{
    if (!gSessionStarted || !gWriter) {
        VideoEncoderCleanup();
        return 0;
    }

    [gVideoInput markAsFinished];

    dispatch_semaphore_t sem = dispatch_semaphore_create(0);
    __block BOOL success = NO;
    [gWriter finishWritingWithCompletionHandler:^{
        success = (gWriter.status == AVAssetWriterStatusCompleted);
        if (!success)
            NSLog(@"[VideoEncoder] finishWriting status=%ld err=%@",
                  (long)gWriter.status, gWriter.error);
        dispatch_semaphore_signal(sem);
    }];
    dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 60 * NSEC_PER_SEC));

    int result = success ? 1 : 0;
    VideoEncoderCleanup();
    return result;
}

void _VideoEncoder_Cancel(void)
{
    if (gWriter && gWriter.status == AVAssetWriterStatusWriting)
        [gWriter cancelWriting];
    VideoEncoderCleanup();
}

} // extern "C"
