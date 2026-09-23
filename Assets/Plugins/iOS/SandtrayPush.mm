#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>
#import <CloudPushSDK/CloudPushSDK.h>
#import "UnityAppController.h"

static NSString *spState=@"not_enabled", *spTap=@"", *spForeground=@"", *spToken=@"";
static BOOL spStarting=NO,spWanted=NO;
static NSUInteger spEpoch=0;
static void SPMain(void (^block)(void)) { dispatch_async(dispatch_get_main_queue(),block); }

@interface SandtrayPushController : UnityAppController <UNUserNotificationCenterDelegate>
@end
@implementation SandtrayPushController
- (BOOL)application:(UIApplication*)app didFinishLaunchingWithOptions:(NSDictionary*)options {
    BOOL result=[super application:app didFinishLaunchingWithOptions:options];
    [UNUserNotificationCenter currentNotificationCenter].delegate=self;
    NSDictionary *notice=options[UIApplicationLaunchOptionsRemoteNotificationKey];
    if(notice)spTap=[[NSString alloc] initWithData:[NSJSONSerialization dataWithJSONObject:notice options:0 error:nil] encoding:NSUTF8StringEncoding];
    return result;
}
- (void)application:(UIApplication*)app didRegisterForRemoteNotificationsWithDeviceToken:(NSData*)token {
    // Unity omits this optional callback when UNITY_USES_REMOTE_NOTIFICATIONS is off.
    if ([UnityAppController instancesRespondToSelector:_cmd])
        [super application:app didRegisterForRemoteNotificationsWithDeviceToken:token];
    if(!spWanted)return;
    [CloudPushSDK registerDevice:token withCallback:^(CloudPushCallbackResult *result) {
        SPMain(^{if(!spWanted)return;spState=result.success?@"ready":@"registration_failed";spToken=result.success?[CloudPushSDK getApnsDeviceToken]:@"";});
    }];
}
- (void)application:(UIApplication*)app didFailToRegisterForRemoteNotificationsWithError:(NSError*)error {
    if ([UnityAppController instancesRespondToSelector:_cmd])
        [super application:app didFailToRegisterForRemoteNotificationsWithError:error];
    spState=@"registration_failed";
}
- (void)userNotificationCenter:(UNUserNotificationCenter*)center willPresentNotification:(UNNotification*)notification withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completion {
    // Active conversations already render incoming messages and badges.
    NSDictionary *data=notification.request.content.userInfo;
    spForeground=[[NSString alloc] initWithData:[NSJSONSerialization dataWithJSONObject:data options:0 error:nil] encoding:NSUTF8StringEncoding];
    completion([data[@"kind"] isEqual:@"schedules"] ? (UNNotificationPresentationOptionAlert | UNNotificationPresentationOptionSound) : UNNotificationPresentationOptionNone);
}
- (void)userNotificationCenter:(UNUserNotificationCenter*)center didReceiveNotificationResponse:(UNNotificationResponse*)response withCompletionHandler:(void (^)(void))completion {
    NSDictionary *data=response.notification.request.content.userInfo;
    spTap=[[NSString alloc] initWithData:[NSJSONSerialization dataWithJSONObject:data options:0 error:nil] encoding:NSUTF8StringEncoding];
    [CloudPushSDK sendNotificationAck:data];completion();
}
@end
IMPL_APP_CONTROLLER_SUBCLASS(SandtrayPushController)

extern "C" {
void SandtrayPushEnable() {
    spWanted=YES;
    if(spStarting)return;spStarting=YES;NSUInteger epoch=++spEpoch;spState=@"requesting_permission";
    [[UNUserNotificationCenter currentNotificationCenter] requestAuthorizationWithOptions:UNAuthorizationOptionAlert|UNAuthorizationOptionSound|UNAuthorizationOptionBadge completionHandler:^(BOOL granted,NSError *error){
        SPMain(^{
            if(epoch!=spEpoch)return;
            if(!granted){spState=@"permission_denied";spStarting=NO;return;}
            NSString *path=[[NSBundle mainBundle] pathForResource:@"SandtrayPushConfig" ofType:@"plist"];
            NSDictionary *config=[NSDictionary dictionaryWithContentsOfFile:path];
            if(!config[@"appKey"] || !config[@"appSecret"]){spState=@"configuration_missing";spStarting=NO;return;}
            [CloudPushSDK startWithAppkey:config[@"appKey"] appSecret:config[@"appSecret"] callback:^(CloudPushCallbackResult *result){
                SPMain(^{if(epoch!=spEpoch)return;spStarting=NO;if(!result.success){spState=@"registration_failed";return;}
                    spState=@"registering";[[UIApplication sharedApplication] registerForRemoteNotifications];});
            }];
        });
    }];
}
void SandtrayPushDisable() {
    spWanted=NO;spEpoch++;spStarting=NO;
    [[UIApplication sharedApplication] unregisterForRemoteNotifications];
    [[UNUserNotificationCenter currentNotificationCenter] removeAllDeliveredNotifications];
    spState=@"not_enabled";spToken=@"";
}
const char* SandtrayPushState(){return [spState UTF8String];}
const char* SandtrayPushDevice(){return [[CloudPushSDK getDeviceId] UTF8String];}
const char* SandtrayPushToken(){return [spToken UTF8String];}
const char* SandtrayPushTap(){return [spTap UTF8String];}
void SandtrayPushClearTap(){spTap=@"";}
const char* SandtrayPushForeground(){return [spForeground UTF8String];}
void SandtrayPushClearForeground(){spForeground=@"";}
void SandtrayPushSettings(){[[UIApplication sharedApplication] openURL:[NSURL URLWithString:UIApplicationOpenSettingsURLString] options:@{} completionHandler:nil];}
const char* SandtrayPushEnvironment(){
    NSString *path=[[NSBundle mainBundle] pathForResource:@"embedded" ofType:@"mobileprovision"];
    NSString *raw=path?[[NSString alloc] initWithData:[NSData dataWithContentsOfFile:path] encoding:NSISOLatin1StringEncoding]:nil;
    NSRange start=[raw rangeOfString:@"<?xml"],end=[raw rangeOfString:@"</plist>"];
    if(raw && start.location!=NSNotFound && end.location!=NSNotFound){
        NSData *xml=[[raw substringWithRange:NSMakeRange(start.location,end.location+end.length-start.location)] dataUsingEncoding:NSISOLatin1StringEncoding];
        NSDictionary *profile=[NSPropertyListSerialization propertyListWithData:xml options:0 format:nil error:nil];
        if([profile[@"Entitlements"][@"aps-environment"] isEqual:@"development"])return "DEV";
    }
    return "PRODUCT";
}
}
