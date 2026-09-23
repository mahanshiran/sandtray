#import <UIKit/UIKit.h>

extern "C" void SandtrayLightHaptic()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 10.0, *)) {
            static UIImpactFeedbackGenerator *generator;
            if (!generator) {
                generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            }
            [generator impactOccurred];
            [generator prepare];
        }
    });
}
