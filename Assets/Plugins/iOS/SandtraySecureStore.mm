#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>

static NSString *const SandtrayCredentialService = @"com.mahanshiran.sandtray.auth";

static NSMutableDictionary *SandtrayQuery(const char *account)
{
    if (account == NULL) return nil;
    NSString *accountName = [NSString stringWithUTF8String:account];
    if (accountName == nil) return nil;
    return [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: SandtrayCredentialService,
        (__bridge id)kSecAttrAccount: accountName
    } mutableCopy];
}

extern "C" int SandtraySecureSet(const char *account, const char *value)
{
    NSMutableDictionary *query = SandtrayQuery(account);
    if (query == nil || value == NULL) return 0;
    NSString *secret = [NSString stringWithUTF8String:value];
    if (secret == nil) return 0;
    NSData *data = [secret dataUsingEncoding:NSUTF8StringEncoding];

    NSDictionary *update = @{(__bridge id)kSecValueData: data};
    OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query,
                                    (__bridge CFDictionaryRef)update);
    if (status == errSecItemNotFound)
    {
        query[(__bridge id)kSecValueData] = data;
        query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
        status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
    }
    return status == errSecSuccess ? 1 : 0;
}

extern "C" char *SandtraySecureGet(const char *account)
{
    NSMutableDictionary *query = SandtrayQuery(account);
    if (query == nil) return NULL;
    query[(__bridge id)kSecReturnData] = @YES;
    query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;

    CFTypeRef result = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
    if (status != errSecSuccess || result == NULL) return NULL;

    NSData *data = CFBridgingRelease(result);
    NSString *secret = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    if (secret == nil) return NULL;
    const char *utf8 = [secret UTF8String];
    if (utf8 == NULL) return NULL;
    return strdup(utf8);
}

extern "C" int SandtraySecureDelete(const char *account)
{
    NSMutableDictionary *query = SandtrayQuery(account);
    if (query == nil) return 0;
    OSStatus status = SecItemDelete((__bridge CFDictionaryRef)query);
    return (status == errSecSuccess || status == errSecItemNotFound) ? 1 : 0;
}

extern "C" void SandtraySecureFree(void *value)
{
    if (value != NULL) free(value);
}
